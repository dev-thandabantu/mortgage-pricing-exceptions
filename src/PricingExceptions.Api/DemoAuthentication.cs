using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PricingExceptions.Api;

public sealed record DemoUser(string Id, string Name, string[] Roles);
public static class DemoUsers
{
    public static readonly DemoUser[] All =
    [
        new("rm-alex", "Alex ? relationship manager", ["RelationshipManager"]),
        new("rm-sam", "Sam ? relationship manager", ["RelationshipManager"]),
        new("reviewer-jules", "Jules ? reviewer", ["Reviewer"]),
        new("reviewer-robin", "Robin ? reviewer", ["Reviewer"]),
        new("rm-reviewer", "Taylor ? RM and reviewer", ["RelationshipManager", "Reviewer"]),
        new("mortgage-process", "Mortgage application process", ["MortgageProcess"])
    ];
}

public sealed class DemoAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-User-Id", out var values))
            return Task.FromResult(AuthenticateResult.NoResult());
        var user = values.Count == 1 ? DemoUsers.All.SingleOrDefault(x => x.Id == values[0]) : null;
        if (user is null) return Task.FromResult(AuthenticateResult.Fail("Unknown demo identity."));
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.Name) };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
