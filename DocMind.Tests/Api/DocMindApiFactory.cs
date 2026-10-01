namespace DocMind.Tests.Api;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

public sealed class DocMindApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
    public string ConnectionString => this.postgres.GetConnectionString();

    public Task InitializeAsync() => this.postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await this.DisposeAsync();
        await this.postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
    _ = builder.UseSetting("ConnectionStrings:DocMind", this.ConnectionString);
}