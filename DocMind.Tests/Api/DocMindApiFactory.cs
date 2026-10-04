namespace DocMind.Tests.Api;

using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

public sealed class DocMindApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestPassword = "Passw0rd!";
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
    public string ConnectionString => this.postgres.GetConnectionString();

    public Task InitializeAsync() => this.postgres.StartAsync();

    // Registers a fresh user and logs in with a session cookie. CreateClient keeps cookies between
    // requests (HandleCookies defaults to true), so every later call on the returned client is
    // authenticated, the same way the browser behaves for the UI. Each call uses a new email
    // because all test classes share one database container.
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = this.CreateClient();
        var email = $"{Guid.NewGuid()}@test.local";

        using var register = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest { Email = email, Password = TestPassword });

        _ = register.EnsureSuccessStatusCode();

        using var login = await client.PostAsJsonAsync(
            "/auth/login?useCookies=true",
            new LoginRequest { Email = email, Password = TestPassword });

        _ = login.EnsureSuccessStatusCode();

        return client;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await this.DisposeAsync();
        await this.postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
    _ = builder.UseSetting("ConnectionStrings:DocMind", this.ConnectionString);
}