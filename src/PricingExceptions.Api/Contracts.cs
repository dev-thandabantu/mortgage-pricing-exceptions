using System.Text.Json;
using System.Text.Json.Serialization;

namespace PricingExceptions.Api;

public sealed record CreateRequest(string ApplicationId, int DiscountBps, string Reason);
public sealed record AmendRequest(int DiscountBps, string Reason, int ExpectedVersion);
public sealed record DecideRequest(string Outcome, int ExpectedVersion, string? Comment);
public sealed record RequestView(string Id, string ApplicationId, string CreatedBy, DateTimeOffset CreatedAt,
    RequestStatus Status, int Version, int RevisionNumber, int DiscountBps, string Reason,
    RevisionView[] Revisions, DecisionView[] Decisions)
{
    public static RequestView From(PricingRequest request) => new(request.Id, request.ApplicationId,
        request.CreatedBy, request.CreatedAt, request.Status, request.Version, request.RevisionNumber,
        request.DiscountBps, request.Reason,
        request.Revisions.OrderBy(x => x.Number).Select(x => new RevisionView(x.RequestId, x.Number, x.DiscountBps, x.Reason, x.Author, x.CreatedAt)).ToArray(),
        request.Decisions.Select(x => new DecisionView(x.Id, x.RequestId, x.RevisionNumber, x.Outcome, x.DecidedBy, x.DecidedAt, x.Comment)).ToArray());
}
public sealed record RevisionView(string RequestId, int Number, int DiscountBps, string Reason, string Author, DateTimeOffset CreatedAt);
public sealed record DecisionView(string Id, string RequestId, int RevisionNumber, RequestStatus Outcome, string DecidedBy, DateTimeOffset DecidedAt, string? Comment);
public sealed record ApprovedDiscount(string ApplicationId, int DiscountBps, string RequestId,
    int RevisionNumber, string DecisionId, string DecidedBy, DateTimeOffset DecidedAt);
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
