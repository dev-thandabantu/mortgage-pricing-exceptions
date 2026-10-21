using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PricingExceptions.Tests;

public sealed class ApiFactory(string databasePath) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseEnvironment("Testing").UseSetting("Database:Path", databasePath);

    public HttpClient As(string? identity = "rm-alex")
    {
        var client = CreateClient();
        if (identity is not null) client.DefaultRequestHeaders.Add("X-User-Id", identity);
        return client;
    }
}
