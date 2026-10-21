using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PricingExceptions.Api;

namespace PricingExceptions.Tests;

public sealed class WorkflowTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "pricing-tests", Guid.NewGuid() + ".db");
    private readonly ApiFactory api;
    public WorkflowTests() => api = new(path);
    public void Dispose()
    {
        api.Dispose();
        if (File.Exists(path)) File.Delete(path);
    }
    private static async Task<HttpResponseMessage> Create(HttpClient client, string key = "test-key", string app = "app-1001", int discount = 25, string? reason = "Customer retention")
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/pricing-exceptions")
        {
            Content = JsonContent.Create(new { applicationId = app, discountBps = discount, reason })
        };
        message.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(message);
    }
    private static async Task<RequestView> View(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RequestView>(ApiJson.Options))!;
    }
    private static Task<HttpResponseMessage> Decide(HttpClient client, RequestView request, string outcome = "Approved")
        => client.PostAsJsonAsync($"/pricing-exceptions/{request.Id}/decisions", new { outcome, expectedVersion = request.Version, comment = "Within delegated authority" });

    [Theory]
    [InlineData("Approved", RequestStatus.Approved)]
    [InlineData("Declined", RequestStatus.Declined)]
    public async Task Decision_records_reviewed_content_actor_and_time(string outcome, RequestStatus expected)
    {
        using var rm = api.As();
        using var reviewer = api.As("reviewer-jules");
        var before = DateTimeOffset.UtcNow;
        var request = await View(await Create(rm));
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.GetAsync("/applications/app-1001/approved-discount")).StatusCode);
        var decided = await View(await Decide(reviewer, request, outcome));
        var fetched = await View(await rm.GetAsync($"/pricing-exceptions/{request.Id}"));
        Assert.Equal(expected, fetched.Status);
        Assert.Equal(2, fetched.Version);
        var decision = Assert.Single(fetched.Decisions);
        Assert.Equal(expected, decision.Outcome);
        Assert.Equal("reviewer-jules", decision.DecidedBy);
        Assert.InRange(decision.DecidedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(1, decision.RevisionNumber);
        var revision = Assert.Single(fetched.Revisions);
        Assert.Equal(25, revision.DiscountBps);
        Assert.Equal("Customer retention", revision.Reason);
        Assert.Equal("rm-alex", revision.Author);
        using var downstream = api.As("mortgage-process");
        var approvedResponse = await downstream.GetAsync("/applications/app-1001/approved-discount");
        if (expected == RequestStatus.Approved)
        {
            var approved = await approvedResponse.Content.ReadFromJsonAsync<ApprovedDiscount>();
            Assert.Equal(HttpStatusCode.OK, approvedResponse.StatusCode);
            Assert.Equal(25, approved!.DiscountBps);
            Assert.Equal(request.Id, approved.RequestId);
            Assert.Equal(decision.Id, approved.DecisionId);
            Assert.Equal(1, approved.RevisionNumber);
        }
        else Assert.Equal(HttpStatusCode.NotFound, approvedResponse.StatusCode);
    }

    [Fact]
    public async Task Retry_replays_exact_creation_response_after_amendment_and_restart()
    {
        using var rm = api.As();
        var original = await Create(rm);
        var json = await original.Content.ReadAsStringAsync();
        var request = await View(original);
        await View(await rm.PatchAsJsonAsync($"/pricing-exceptions/{request.Id}", new { discountBps = 35, reason = "Revised offer", expectedVersion = 1 }));
        api.Dispose();
        using var restarted = new ApiFactory(path);
        using var restartedRm = restarted.As();
        var replay = await Create(restartedRm);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(json, await replay.Content.ReadAsStringAsync());
        Assert.Equal(original.Headers.Location, replay.Headers.Location);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        var current = await View(await restartedRm.GetAsync($"/pricing-exceptions/{request.Id}"));
        Assert.Equal(35, current.DiscountBps);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task Fingerprint_uses_parsed_fields_rather_than_json_layout()
    {
        using var rm = api.As();
        var original = await Create(rm);
        using var retry = new HttpRequestMessage(HttpMethod.Post, "/pricing-exceptions")
        {
            Content = new StringContent("{ \"reason\": \"Customer retention\", \"discountBps\":25, \"applicationId\":\"app-1001\" }", Encoding.UTF8, "application/json")
        };
        retry.Headers.Add("Idempotency-Key", "test-key");
        var replay = await rm.SendAsync(retry);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await original.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Key_conflicts_are_rejected_and_keys_are_scoped_to_actor()
    {
        using var first = api.As();
        using var second = api.As("rm-sam");
        await View(await Create(first));
        Assert.Equal(HttpStatusCode.Conflict, (await Create(first, discount: 30)).StatusCode);
        var other = await View(await Create(second, app: "app-1002"));
        Assert.Equal("rm-sam", other.CreatedBy);
    }

    [Fact]
    public async Task Concurrent_duplicate_calls_create_one_request_and_replay_the_winner()
    {
        using var first = api.As();
        using var second = api.As();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new[] { first, second }.Select(client => Task.Run(async () => { await gate.Task; return await Create(client); })).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(calls);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync());
        Assert.Equal(new[] { "false", "true" }, responses.Select(x => x.Headers.GetValues("Idempotency-Replayed").Single()).Order().ToArray());
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDb>();
        Assert.Equal(1, await db.Requests.CountAsync());
        Assert.Equal(1, await db.Revisions.CountAsync());
        Assert.Equal(1, await db.Idempotency.CountAsync());
    }

    [Fact]
    public async Task Competing_decisions_commit_one_outcome_and_one_history_entry()
    {
        using var rm = api.As();
        var request = await View(await Create(rm));
        using var first = api.As("reviewer-jules");
        using var second = api.As("reviewer-robin");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = Task.Run(async () => { await gate.Task; return await Decide(first, request); });
        var b = Task.Run(async () => { await gate.Task; return await Decide(second, request, "Declined"); });
        gate.SetResult();
        var responses = await Task.WhenAll(a, b);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var current = await View(await rm.GetAsync($"/pricing-exceptions/{request.Id}"));
        var decision = Assert.Single(current.Decisions);
        Assert.Equal(current.Status, decision.Outcome);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task Amendment_preserves_revisions_and_stale_decision_cannot_approve_new_terms()
    {
        using var rm = api.As();
        using var reviewer = api.As("reviewer-jules");
        var original = await View(await Create(rm));
        var amended = await View(await rm.PatchAsJsonAsync($"/pricing-exceptions/{original.Id}", new { discountBps = 40, reason = "Corrected discount", expectedVersion = 1 }));
        Assert.Equal(2, amended.Revisions.Length);
        Assert.Equal(25, amended.Revisions[0].DiscountBps);
        Assert.Equal("Customer retention", amended.Revisions[0].Reason);
        Assert.Equal(40, amended.Revisions[1].DiscountBps);
        Assert.Equal("Corrected discount", amended.Revisions[1].Reason);
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(reviewer, original)).StatusCode);
        var approved = await View(await Decide(reviewer, amended));
        Assert.Equal(2, Assert.Single(approved.Decisions).RevisionNumber);
        Assert.Equal(HttpStatusCode.Conflict, (await rm.PatchAsJsonAsync($"/pricing-exceptions/{original.Id}", new { discountBps = 50, reason = "Too late", expectedVersion = approved.Version })).StatusCode);
        var discount = await reviewer.GetFromJsonAsync<ApprovedDiscount>("/applications/app-1001/approved-discount");
        Assert.Equal(40, discount!.DiscountBps);
        Assert.Equal(2, discount.RevisionNumber);
    }

    [Fact]
    public async Task Concurrent_amendment_and_decision_cannot_both_commit()
    {
        using var rm = api.As();
        using var reviewer = api.As("reviewer-jules");
        var request = await View(await Create(rm));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var amend = Task.Run(async () => { await gate.Task; return await rm.PatchAsJsonAsync($"/pricing-exceptions/{request.Id}", new { discountBps = 45, reason = "New terms", expectedVersion = 1 }); });
        var decide = Task.Run(async () => { await gate.Task; return await Decide(reviewer, request); });
        gate.SetResult();
        var responses = await Task.WhenAll(amend, decide);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var current = await View(await rm.GetAsync($"/pricing-exceptions/{request.Id}"));
        if (current.Status == RequestStatus.Approved)
        {
            Assert.Equal(25, current.DiscountBps);
            Assert.Single(current.Revisions);
            Assert.Single(current.Decisions);
        }
        else
        {
            Assert.Equal(45, current.DiscountBps);
            Assert.Equal(2, current.Revisions.Length);
            Assert.Empty(current.Decisions);
        }
    }

    [Fact]
    public async Task History_write_failure_rolls_back_the_state_transition()
    {
        using var rm = api.As();
        using var reviewer = api.As("reviewer-jules");
        var request = await View(await Create(rm));
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PricingDb>();
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectDecision BEFORE INSERT ON Decisions BEGIN SELECT RAISE(ABORT, 'injected history failure'); END;");
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(reviewer, request)).StatusCode);
        var current = await View(await rm.GetAsync($"/pricing-exceptions/{request.Id}"));
        Assert.Equal(RequestStatus.Pending, current.Status);
        Assert.Equal(1, current.Version);
        Assert.Empty(current.Decisions);
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PricingDb>().Database.ExecuteSqlRawAsync("DROP TRIGGER RejectDecision;");
        Assert.Equal(RequestStatus.Approved, (await View(await Decide(reviewer, request))).Status);
    }

    [Fact]
    public async Task Authorisation_ownership_and_four_eyes_are_separate_boundaries()
    {
        using var anonymous = api.As(null);
        using var unknown = api.As("unknown");
        using var rm = api.As();
        using var otherRm = api.As("rm-sam");
        using var reviewer = api.As("reviewer-jules");
        using var dualRole = api.As("rm-reviewer");
        using var process = api.As("mortgage-process");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/pricing-exceptions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await unknown.GetAsync("/pricing-exceptions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Create(reviewer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Create(process)).StatusCode);
        var request = await View(await Create(rm));
        Assert.Equal(HttpStatusCode.Forbidden, (await Decide(rm, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherRm.PatchAsJsonAsync($"/pricing-exceptions/{request.Id}", new { discountBps = 30, reason = "Not mine", expectedVersion = 1 })).StatusCode);
        var own = await View(await Create(dualRole, app: "app-1002"));
        Assert.Equal(HttpStatusCode.Forbidden, (await Decide(dualRole, own)).StatusCode);
        Assert.Equal(RequestStatus.Approved, (await View(await Decide(reviewer, own))).Status);
    }

    [Fact]
    public async Task Only_decline_releases_the_application_for_resubmission()
    {
        using var rm = api.As();
        using var reviewer = api.As("reviewer-jules");
        var first = await View(await Create(rm));
        Assert.Equal(HttpStatusCode.Conflict, (await Create(rm, "new-key")).StatusCode);
        await View(await Decide(reviewer, first, "Declined"));
        var next = await View(await Create(rm, "new-key"));
        Assert.NotEqual(first.Id, next.Id);
        await View(await Decide(reviewer, next));
        Assert.Equal(HttpStatusCode.Conflict, (await Create(rm, "third-key")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(reviewer, next)).StatusCode);
    }

    [Theory]
    [InlineData(0, "Reason")]
    [InlineData(-1, "Reason")]
    [InlineData(101, "Reason")]
    [InlineData(25, " ")]
    [InlineData(25, null)]
    public async Task Invalid_pricing_does_not_consume_the_idempotency_key(int discount, string? reason)
    {
        using var rm = api.As();
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(rm, discount: discount, reason: reason)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Create(rm)).StatusCode);
    }

    [Fact]
    public async Task Missing_application_headers_and_unknown_outcome_are_explicit_errors()
    {
        using var rm = api.As();
        using var reviewer = api.As("reviewer-jules");
        Assert.Equal(HttpStatusCode.NotFound, (await Create(rm, app: "missing")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await rm.PostAsJsonAsync("/pricing-exceptions", new { applicationId = "app-1001", discountBps = 25, reason = "Reason" })).StatusCode);
        var request = await View(await Create(rm));
        Assert.Equal(HttpStatusCode.BadRequest, (await Decide(reviewer, request, "Pending")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.GetAsync("/applications/missing/approved-discount")).StatusCode);
        var problem = await (await reviewer.GetAsync("/applications/app-1001/approved-discount")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("No approved", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Database_constraint_also_blocks_duplicate_active_requests()
    {
        using var rm = api.As();
        await View(await Create(rm));
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDb>();
        db.Requests.Add(PricingRequest.Create("app-1001", 20, "Bypass HTTP precheck", "rm-sam", DateTimeOffset.UtcNow, 100));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(19, Assert.IsType<SqliteException>(error.InnerException).SqliteErrorCode);
    }
}
