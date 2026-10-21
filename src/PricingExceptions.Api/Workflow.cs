using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace PricingExceptions.Api;

public sealed class Workflow(PricingDb db, TimeProvider clock, IConfiguration configuration)
{
    private int Cap => configuration.GetValue("Pricing:MaxDiscountBps", 100);

    public async Task<(string Json, string Location, bool Replayed)> Create(CreateRequest input,
        string actor, string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(char.IsControl))
            throw new RequestError(400, "A single Idempotency-Key of 1-128 non-control characters is required.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input, ApiJson.Options))));
        // Acquire SQLite's write reservation BEFORE reading; separate processes share this lock.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var previous = await db.Idempotency.FindAsync([actor, "CreatePricingException", key], cancellationToken);
        if (previous is not null)
        {
            if (previous.Fingerprint != fingerprint) throw new RequestError(409, "Idempotency-Key was already used with different request fields.");
            return (previous.ResponseJson, previous.Location, true);
        }
        if (string.IsNullOrWhiteSpace(input.ApplicationId))
            throw new RequestError(400, "ApplicationId is required.");
        if (!await db.Applications.AnyAsync(x => x.Id == input.ApplicationId, cancellationToken))
            throw new RequestError(404, "Mortgage application not found.");
        if (await db.Requests.AnyAsync(x => x.ApplicationId == input.ApplicationId &&
            (x.Status == RequestStatus.Pending || x.Status == RequestStatus.Approved), cancellationToken))
            throw new RequestError(409, "This application already has a pending or approved request.");
        var request = PricingRequest.Create(input.ApplicationId, input.DiscountBps, input.Reason, actor, clock.GetUtcNow(), Cap);
        var json = JsonSerializer.Serialize(RequestView.From(request), ApiJson.Options);
        var location = $"/pricing-exceptions/{request.Id}";
        db.Requests.Add(request);
        db.Idempotency.Add(new() { Actor = actor, Operation = "CreatePricingException", Key = key,
            Fingerprint = fingerprint, Location = location, ResponseJson = json });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (json, location, false);
    }

    public async Task<RequestView> Amend(string id, AmendRequest input, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var request = await Load(id, cancellationToken);
        request.Amend(input.DiscountBps, input.Reason, input.ExpectedVersion, actor, clock.GetUtcNow(), Cap);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RequestView.From(request);
    }

    public async Task<RequestView> Decide(string id, DecideRequest input, string actor, CancellationToken cancellationToken)
    {
        if (input.Outcome is not ("Approved" or "Declined"))
            throw new RequestError(400, "Outcome must be Approved or Declined.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var request = await Load(id, cancellationToken);
        request.Decide(Enum.Parse<RequestStatus>(input.Outcome), input.ExpectedVersion, actor, clock.GetUtcNow(), input.Comment);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RequestView.From(request);
    }

    public async Task<RequestView> Get(string id, CancellationToken cancellationToken)
        => RequestView.From(await Load(id, cancellationToken));

    public async Task<ApprovedDiscount> GetApproved(string applicationId, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(x => x.Id == applicationId, cancellationToken))
            throw new RequestError(404, "Mortgage application not found.");
        var request = await db.Requests.AsNoTracking().Include(x => x.Revisions).Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.ApplicationId == applicationId && x.Status == RequestStatus.Approved, cancellationToken);
        if (request is null) throw new RequestError(404, "No approved pricing exception exists for this application.");
        var decision = request.Decisions.Single();
        var reviewed = request.Revisions.Single(x => x.Number == decision.RevisionNumber);
        return new(applicationId, reviewed.DiscountBps, request.Id, reviewed.Number,
            decision.Id, decision.DecidedBy, decision.DecidedAt);
    }

    private async Task<PricingRequest> Load(string id, CancellationToken cancellationToken)
        => await db.Requests.Include(x => x.Revisions).Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new RequestError(404, "Pricing exception request not found.");
}
