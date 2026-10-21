namespace PricingExceptions.Api;

public enum RequestStatus { Pending, Approved, Declined }

public sealed class PricingRequest
{
    private PricingRequest() { }
    public string Id { get; private set; } = Guid.NewGuid().ToString("N");
    public string ApplicationId { get; private set; } = "";
    public string CreatedBy { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public RequestStatus Status { get; private set; } = RequestStatus.Pending;
    public int Version { get; private set; } = 1;
    public int RevisionNumber { get; private set; } = 1;
    public int DiscountBps { get; private set; }
    public string Reason { get; private set; } = "";
    public List<RequestRevision> Revisions { get; private set; } = [];
    public List<RequestDecision> Decisions { get; private set; } = [];

    public static PricingRequest Create(string applicationId, int discountBps, string reason,
        string actor, DateTimeOffset now, int cap)
    {
        ValidatePricing(discountBps, reason, cap);
        var request = new PricingRequest
        {
            ApplicationId = applicationId, DiscountBps = discountBps,
            Reason = reason.Trim(), CreatedBy = actor, CreatedAt = now
        };
        request.Revisions.Add(new(request.Id, 1, discountBps, request.Reason, actor, now));
        return request;
    }

    public void Amend(int discountBps, string reason, int expectedVersion, string actor,
        DateTimeOffset now, int cap)
    {
        if (actor != CreatedBy) throw new RequestError(403, "Only the submitting RM may amend this request.");
        EnsurePendingVersion(expectedVersion);
        ValidatePricing(discountBps, reason, cap);
        DiscountBps = discountBps;
        Reason = reason.Trim();
        RevisionNumber++;
        Version++;
        Revisions.Add(new(Id, RevisionNumber, discountBps, Reason, actor, now));
    }

    public void Decide(RequestStatus outcome, int expectedVersion, string actor,
        DateTimeOffset now, string? comment)
    {
        if (actor == CreatedBy) throw new RequestError(403, "A requester cannot decide their own request.");
        EnsurePendingVersion(expectedVersion);
        if (outcome is not (RequestStatus.Approved or RequestStatus.Declined))
            throw new RequestError(400, "Outcome must be Approved or Declined.");
        if (comment?.Length > 2000) throw new RequestError(400, "Comment cannot exceed 2000 characters.");
        Status = outcome;
        Version++;
        Decisions.Add(new(Id, RevisionNumber, outcome, actor, now, comment?.Trim()));
    }

    private void EnsurePendingVersion(int expectedVersion)
    {
        if (expectedVersion < 1) throw new RequestError(400, "A positive expectedVersion is required.");
        if (Status != RequestStatus.Pending) throw new RequestError(409, "The request is already decided.");
        if (Version != expectedVersion) throw new RequestError(409, "The request changed. Reload it before continuing.");
    }

    private static void ValidatePricing(int discountBps, string? reason, int cap)
    {
        if (discountBps <= 0 || discountBps > cap)
            throw new RequestError(400, $"Discount must be between 1 and {cap} basis points inclusive.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            throw new RequestError(400, "Reason is required and cannot exceed 2000 characters.");
    }
}

public sealed class RequestRevision
{
    private RequestRevision() { }
    public RequestRevision(string requestId, int number, int discountBps, string reason, string author, DateTimeOffset createdAt)
        => (RequestId, Number, DiscountBps, Reason, Author, CreatedAt) = (requestId, number, discountBps, reason, author, createdAt);
    public string RequestId { get; private set; } = "";
    public int Number { get; private set; }
    public int DiscountBps { get; private set; }
    public string Reason { get; private set; } = "";
    public string Author { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class RequestDecision
{
    private RequestDecision() { }
    public RequestDecision(string requestId, int revisionNumber, RequestStatus outcome, string decidedBy, DateTimeOffset decidedAt, string? comment)
        => (RequestId, RevisionNumber, Outcome, DecidedBy, DecidedAt, Comment) = (requestId, revisionNumber, outcome, decidedBy, decidedAt, comment);
    public string Id { get; private set; } = Guid.NewGuid().ToString("N");
    public string RequestId { get; private set; } = "";
    public int RevisionNumber { get; private set; }
    public RequestStatus Outcome { get; private set; }
    public string DecidedBy { get; private set; } = "";
    public DateTimeOffset DecidedAt { get; private set; }
    public string? Comment { get; private set; }
}

public sealed class RequestError(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
