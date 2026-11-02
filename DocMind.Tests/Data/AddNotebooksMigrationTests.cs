namespace DocMind.Tests.Data;

using DocMind.Core.Data;
using DocMind.Tests.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

// AddNotebooks is written by hand to keep existing rows, and the shared test database is empty when
// it is migrated, so nothing else would exercise that SQL. This test uses a database of its own,
// stopped one migration short, the way a database with real data would be.
[Collection(ApiCollectionDefinition.Name)]
public class AddNotebooksMigrationTests(DocMindApiFactory factory)
{
    private const string PreviousMigration = "20261013230913_AddConversations";

    [Fact]
    public async Task ExistingContentMovesToOneNotebookPerUser()
    {
        var connectionString = await this.CreateDatabaseAsync();
        await using var dbContext = new DocMindDbContext(new DbContextOptionsBuilder<DocMindDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var userWithoutContentId = Guid.NewGuid();
        await InsertUserAsync(dbContext, ownerId);
        await InsertUserAsync(dbContext, otherUserId);
        await InsertUserAsync(dbContext, userWithoutContentId);
        var (ownerDocumentId, ownerConversationId) = await InsertContentAsync(dbContext, ownerId);
        var (otherDocumentId, _) = await InsertContentAsync(dbContext, otherUserId);

        await migrator.MigrateAsync();

        var notebooks = await dbContext.Notebooks.ToListAsync();
        Assert.Equal(3, notebooks.Count);
        Assert.All(notebooks, notebook => Assert.Equal("My notebook", notebook.Title));
        Assert.Contains(notebooks, notebook => notebook.UserId == userWithoutContentId);

        var ownerNotebookId = notebooks.Single(notebook => notebook.UserId == ownerId).Id;
        var otherNotebookId = notebooks.Single(notebook => notebook.UserId == otherUserId).Id;
        Assert.Equal(ownerNotebookId, await NotebookOfDocumentAsync(dbContext, ownerDocumentId));
        Assert.Equal(otherNotebookId, await NotebookOfDocumentAsync(dbContext, otherDocumentId));
        Assert.Equal(
            ownerNotebookId,
            await dbContext.DocumentChunks.Where(chunk => chunk.DocumentId == ownerDocumentId).Select(chunk => chunk.NotebookId).SingleAsync());
        Assert.Equal(
            ownerNotebookId,
            await dbContext.Conversations.Where(conversation => conversation.Id == ownerConversationId).Select(conversation => conversation.NotebookId).SingleAsync());
    }

    private static Task<Guid> NotebookOfDocumentAsync(DocMindDbContext dbContext, Guid documentId) =>
        dbContext.Documents.Where(document => document.Id == documentId).Select(document => document.NotebookId).SingleAsync();

    private static async Task InsertUserAsync(DocMindDbContext dbContext, Guid userId) =>
        _ = await dbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO "AspNetUsers" ("Id", "AccessFailedCount", "EmailConfirmed", "LockoutEnabled", "PhoneNumberConfirmed", "TwoFactorEnabled")
            VALUES ({userId}, 0, FALSE, FALSE, FALSE, FALSE)
            """);

    // Plain SQL, since the entities describe the schema after the migration rather than before it.
    private static async Task<(Guid DocumentId, Guid ConversationId)> InsertContentAsync(DocMindDbContext dbContext, Guid userId)
    {
        var documentId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var sha256 = new string('a', 64);

        _ = await dbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO "Documents" ("Id", "UserId", "FileName", "SizeBytes", "Sha256", "EmbeddingModel", "CreatedAt")
            VALUES ({documentId}, {userId}, 'manual.pdf', 3, {sha256}, 'test', now())
            """);
        _ = await dbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO "DocumentChunks" ("Id", "DocumentId", "UserId", "SequenceNumber", "Content", "TokenCount", "Embedding")
            VALUES ({Guid.NewGuid()}, {documentId}, {userId}, 0, 'content', 1, array_fill(1::real, ARRAY[768])::vector)
            """);
        _ = await dbContext.Database.ExecuteSqlAsync($"""
            INSERT INTO "Conversations" ("Id", "UserId", "Title", "CreatedAt", "UpdatedAt")
            VALUES ({conversationId}, {userId}, 'A question', now(), now())
            """);

        return (documentId, conversationId);
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(factory.ConnectionString) { Database = $"migration_{Guid.NewGuid():N}" };

        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"""CREATE DATABASE "{builder.Database}" """, connection);
        _ = await command.ExecuteNonQueryAsync();

        return builder.ConnectionString;
    }
}
