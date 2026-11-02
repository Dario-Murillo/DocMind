namespace DocMind.Tests.Notebooks;

using DocMind.Core.Data;
using DocMind.Core.Notebooks;
using DocMind.Tests.Api;
using Microsoft.EntityFrameworkCore;

// Runs against the Postgres container shared by the Api collection: deleting a notebook is done by
// the database's cascades, which only a real database can show.
[Collection(ApiCollectionDefinition.Name)]
public class NotebookServiceTests(DocMindApiFactory factory)
{
    [Fact]
    public async Task CreateNotebookAsyncTrimsTitleAndStartsEmpty()
    {
        var userId = await factory.CreateUserAsync();

        var notebook = await this.UseServiceAsync(service => service.CreateNotebookAsync(userId, "  Thesis research  "));

        Assert.Equal("Thesis research", notebook.Title);
        Assert.Equal(0, notebook.DocumentCount);
        await using var assertContext = factory.CreateDbContext();
        Assert.True(await assertContext.Notebooks.AnyAsync(stored => stored.Id == notebook.Id && stored.UserId == userId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateNotebookAsyncNullOrEmptyTitleThrowsArgumentException(string? title) =>
        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            this.UseServiceAsync(service => service.CreateNotebookAsync(Guid.NewGuid(), title!)));

    [Fact]
    public async Task CreateNotebookAsyncTitleAboveLimitThrowsArgumentException() =>
        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            this.UseServiceAsync(service => service.CreateNotebookAsync(Guid.NewGuid(), new string('t', Notebook.MaxTitleLength + 1))));

    [Fact]
    public async Task ListNotebooksAsyncReturnsOwnNotebooksNewestFirstWithDocumentCount()
    {
        var userId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var older = await factory.SeedNotebookAsync(userId, "Older");
        _ = await factory.SeedNotebookAsync(userId, "Newer");
        _ = await factory.SeedNotebookAsync(otherUserId, "Someone else's");
        _ = await factory.SeedDocumentAsync(userId, "first.pdf", notebookId: older);
        _ = await factory.SeedDocumentAsync(userId, "second.pdf", notebookId: older);

        var notebooks = await this.UseServiceAsync(service => service.ListNotebooksAsync(userId));

        // SeedDocumentAsync was given a notebook each time, so no extra notebooks appear here.
        Assert.Equal(["Newer", "Older"], notebooks.Select(notebook => notebook.Title));
        Assert.Equal([0, 2], notebooks.Select(notebook => notebook.DocumentCount));
    }

    [Fact]
    public async Task GetNotebookAsyncNotebookOfAnotherUserReturnsNull()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(ownerId);

        var notebook = await this.UseServiceAsync(service => service.GetNotebookAsync(otherUserId, notebookId));

        Assert.Null(notebook);
    }

    [Fact]
    public async Task RenameNotebookAsyncChangesTitleAndKeepsDocumentCount()
    {
        var userId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(userId, "Old title");
        _ = await factory.SeedDocumentAsync(userId, notebookId: notebookId);

        var renamed = await this.UseServiceAsync(service => service.RenameNotebookAsync(userId, notebookId, " New title "));

        Assert.NotNull(renamed);
        Assert.Equal("New title", renamed.Title);
        Assert.Equal(1, renamed.DocumentCount);
    }

    [Fact]
    public async Task RenameNotebookAsyncNotebookOfAnotherUserReturnsNullAndKeepsTitle()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(ownerId, "Original");

        var renamed = await this.UseServiceAsync(service => service.RenameNotebookAsync(otherUserId, notebookId, "Taken over"));

        Assert.Null(renamed);
        await using var assertContext = factory.CreateDbContext();
        Assert.Equal("Original", await assertContext.Notebooks.Where(notebook => notebook.Id == notebookId).Select(notebook => notebook.Title).SingleAsync());
    }

    [Fact]
    public async Task DeleteNotebookAsyncRemovesItsContentAndLeavesOtherNotebooksAlone()
    {
        var userId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(userId);
        var otherNotebookId = await factory.SeedNotebookAsync(userId);
        var documentId = await factory.SeedDocumentAsync(userId, notebookId: notebookId);
        var conversationId = await factory.SeedConversationAsync(userId, citedDocumentId: documentId);
        var otherDocumentId = await factory.SeedDocumentAsync(userId, notebookId: otherNotebookId);
        var otherConversationId = await factory.SeedConversationAsync(userId, citedDocumentId: otherDocumentId);

        var deleted = await this.UseServiceAsync(service => service.DeleteNotebookAsync(userId, notebookId));

        Assert.True(deleted);
        await using var assertContext = factory.CreateDbContext();
        Assert.False(await assertContext.Notebooks.AnyAsync(notebook => notebook.Id == notebookId));
        Assert.False(await assertContext.Documents.AnyAsync(document => document.Id == documentId));
        Assert.False(await assertContext.DocumentFiles.AnyAsync(file => file.DocumentId == documentId));
        Assert.False(await assertContext.DocumentChunks.AnyAsync(chunk => chunk.DocumentId == documentId));
        Assert.False(await assertContext.Conversations.AnyAsync(conversation => conversation.Id == conversationId));
        Assert.False(await assertContext.Messages.AnyAsync(message => message.ConversationId == conversationId));
        Assert.False(await assertContext.MessageSources.AnyAsync(source => source.DocumentId == documentId));

        Assert.True(await assertContext.Documents.AnyAsync(document => document.Id == otherDocumentId));
        var surviving = await SourcesOfAsync(assertContext, otherConversationId);
        Assert.Equal([otherDocumentId], surviving);
    }

    [Fact]
    public async Task DeleteNotebookAsyncNotebookOfAnotherUserReturnsFalseAndKeepsIt()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(ownerId);

        var deleted = await this.UseServiceAsync(service => service.DeleteNotebookAsync(otherUserId, notebookId));

        Assert.False(deleted);
        await using var assertContext = factory.CreateDbContext();
        Assert.True(await assertContext.Notebooks.AnyAsync(notebook => notebook.Id == notebookId));
    }

    private static Task<List<Guid?>> SourcesOfAsync(DocMindDbContext dbContext, Guid conversationId) =>
        dbContext.MessageSources
            .Where(source => dbContext.Messages.Any(message => message.Id == source.MessageId && message.ConversationId == conversationId))
            .Select(source => source.DocumentId)
            .ToListAsync();

    // Each call gets its own DbContext, the way each request gets its own scope in the API.
    private async Task<T> UseServiceAsync<T>(Func<NotebookService, Task<T>> action)
    {
        await using var dbContext = factory.CreateDbContext();
        return await action(new NotebookService(dbContext));
    }
}
