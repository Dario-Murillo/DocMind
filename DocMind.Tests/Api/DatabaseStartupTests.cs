namespace DocMind.Tests.Api;

using Npgsql;

[Collection(ApiCollectionDefinition.Name)]
public class DatabaseStartupTests(DocMindApiFactory factory)
{
    [Fact]
    public async Task StartupInstallsVectorExtension()
    {
        // Creating a client boots the app, which applies the migrations on startup.
        using var client = factory.CreateClient();

        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_extension WHERE extname = 'vector'", connection);
        var count = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(1L, count);
    }
}
