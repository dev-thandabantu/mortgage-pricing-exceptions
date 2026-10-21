using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PricingExceptions.Api;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("Demo identity is allowed only in Development or Testing. Configure real authentication before production use.");
var cap = builder.Configuration.GetValue("Pricing:MaxDiscountBps", 100);
if (cap <= 0) throw new InvalidOperationException("Pricing:MaxDiscountBps must be positive.");
var databasePath = builder.Configuration["Database:Path"] ?? Path.Combine(builder.Environment.ContentRootPath, "state", "pricing.db");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, DefaultTimeout = 5, Pooling = false }.ToString();
builder.Services.AddDbContext<PricingDb>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Workflow>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAuthentication("Demo").AddScheme<AuthenticationSchemeOptions, DemoAuthentication>("Demo", _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RM", policy => policy.RequireRole("RelationshipManager"));
    options.AddPolicy("Reviewer", policy => policy.RequireRole("Reviewer"));
    options.AddPolicy("MortgageRead", policy => policy.RequireRole("MortgageProcess", "Reviewer", "RelationshipManager"));
});
builder.Services.AddOpenApi();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<PricingDb>();
    database.Database.EnsureCreated();
}
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (RequestError error) { await Problem(error.StatusCode, error.Message); }
    catch (DbUpdateConcurrencyException) { await Problem(409, "The request changed. Reload it before continuing."); }
    catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteErrorCode: 19 })
    { await Problem(409, "The operation conflicts with an existing request or decision."); }
    catch (Exception error) when (error is SqliteException { SqliteErrorCode: 5 or 6 } ||
        error is DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 5 or 6 } })
    {
        context.Response.Headers.RetryAfter = "1";
        await Problem(503, "Database is busy. Retry the operation with the same idempotency key or version.");
    }
    async Task Problem(int status, string detail) => await Results.Problem(statusCode: status, detail: detail).ExecuteAsync(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/demo", async (PricingDb db, CancellationToken ct) => Results.Ok(new
{
    users = DemoUsers.All, applications = await db.Applications.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct), maxDiscountBps = cap
}));
var api = app.MapGroup("").RequireAuthorization();
api.MapPost("/pricing-exceptions", async (CreateRequest input, HttpContext context, Workflow workflow, CancellationToken ct) =>
{
    var keys = context.Request.Headers["Idempotency-Key"];
    if (keys.Count != 1) throw new RequestError(400, "A single Idempotency-Key header is required.");
    var result = await workflow.Create(input, Actor(context), keys[0]!, ct);
    context.Response.Headers.Location = result.Location;
    context.Response.Headers["Idempotency-Replayed"] = result.Replayed ? "true" : "false";
    return Results.Content(result.Json, "application/json", statusCode: 201);
}).RequireAuthorization("RM").Produces<RequestView>(201).ProducesProblem(400).ProducesProblem(409).WithName("CreatePricingException");
api.MapGet("/pricing-exceptions/{id}", async (string id, Workflow workflow, CancellationToken ct) => await workflow.Get(id, ct))
    .WithName("GetPricingException");
api.MapGet("/pricing-exceptions", async (PricingDb db, CancellationToken ct) =>
{
    // Stable, bounded list for the exercise UI; full history is retrieved by id.
    var requests = await db.Requests.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(100)
        .Select(x => new { x.Id, x.ApplicationId, x.Status, x.Version, x.DiscountBps, x.CreatedBy, x.CreatedAt }).ToListAsync(ct);
    return Results.Ok(requests);
}).WithName("ListPricingExceptions");
api.MapPatch("/pricing-exceptions/{id}", async (string id, AmendRequest input, HttpContext context, Workflow workflow, CancellationToken ct)
    => await workflow.Amend(id, input, Actor(context), ct)).RequireAuthorization("RM").WithName("AmendPricingException");
api.MapPost("/pricing-exceptions/{id}/decisions", async (string id, DecideRequest input, HttpContext context, Workflow workflow, CancellationToken ct)
    => await workflow.Decide(id, input, Actor(context), ct)).RequireAuthorization("Reviewer").WithName("DecidePricingException");
api.MapGet("/applications/{id}/approved-discount", async (string id, Workflow workflow, CancellationToken ct)
    => await workflow.GetApproved(id, ct)).RequireAuthorization("MortgageRead").WithName("GetApprovedDiscount");
app.Run();
static string Actor(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
public partial class Program;
