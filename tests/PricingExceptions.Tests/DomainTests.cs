using PricingExceptions.Api;

namespace PricingExceptions.Tests;

public sealed class DomainTests
{
    [Fact]
    public void Amendment_rejects_a_stale_version_without_changing_history()
    {
        var request = PricingRequest.Create("app", 25, "Original", "rm", DateTimeOffset.UtcNow, 100);
        request.Amend(30, "Correction", 1, "rm", DateTimeOffset.UtcNow, 100);
        var error = Assert.Throws<RequestError>(() => request.Amend(35, "Lost update", 1, "rm", DateTimeOffset.UtcNow, 100));
        Assert.Equal(409, error.StatusCode);
        Assert.Equal(30, request.DiscountBps);
        Assert.Equal(2, request.Revisions.Count);
    }

    [Fact]
    public void Configured_cap_is_inclusive_and_reason_is_bounded()
    {
        Assert.Equal(40, PricingRequest.Create("app", 40, "Reason", "rm", DateTimeOffset.UtcNow, 40).DiscountBps);
        Assert.Throws<RequestError>(() => PricingRequest.Create("app", 41, "Reason", "rm", DateTimeOffset.UtcNow, 40));
        Assert.Throws<RequestError>(() => PricingRequest.Create("app", 25, new string('x', 2001), "rm", DateTimeOffset.UtcNow, 40));
    }
}
