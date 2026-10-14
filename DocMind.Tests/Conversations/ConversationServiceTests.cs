namespace DocMind.Tests.Conversations;

using DocMind.Core.Chunking;
using DocMind.Core.Conversations;
using DocMind.Core.Query;
using DocMind.Core.VectorStore;
using DocMind.Tests.Api;
using Microsoft.EntityFrameworkCore;

// Runs against the Postgres container shared by the Api collection, like DocumentServiceTests: what
// matters is what ends up in the database (one SaveChangesAsync per exchange, the cascades, SET NULL
// when a cited document is deleted). The RAG pipeline is faked, so Ollama isn't needed.
[Collection(ApiCollectionDefinition.Name)]
public class ConversationServiceTests(DocMindApiFactory factory)
{
    private const string Answer = "DocMind answers questions about your documents.";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartConversationAsyncNullOrEmptyQuestionThrowsArgumentException(string? question) =>
        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            this.UseServiceAsync(service => service.StartConversationAsync(Guid.NewGuid(), question!)));

    [Fact]
    public async Task StartConversationAsyncStoresQuestionAnswerAndSources()
    {
        var userId = await factory.CreateUserAsync();
        var documentId = await factory.SeedDocumentAsync(userId, "manual.pdf");

        // Lowest score first, so the assertion below checks GetConversationAsync's ordering rather
        // than the order the sources were inserted in.
        var queryService = new FakeQueryService(
        [
            SourceFrom(documentId, sequenceNumber: 1, score: 0.5f),
            SourceFrom(documentId, sequenceNumber: 0, score: 0.9f, content: new string('a', 200)),
        ]);

        var exchange = await this.UseServiceAsync(
            service => service.StartConversationAsync(userId, "What is DocMind?"),
            queryService);

        var conversation = await this.UseServiceAsync(service => service.GetConversationAsync(userId, exchange.Conversation.Id));
        Assert.NotNull(conversation);
        Assert.Equal("What is DocMind?", conversation.Title);
        Assert.Collection(
            conversation.Messages,
            question =>
            {
                Assert.Equal(MessageRole.User, question.Role);
                Assert.Equal("What is DocMind?", question.Content);
                Assert.Empty(question.Sources);
            },
            answer =>
            {
                Assert.Equal(MessageRole.Assistant, answer.Role);
                Assert.Equal(Answer, answer.Content);
                Assert.Equal([0, 1], answer.Sources.Select(source => source.SequenceNumber));
                Assert.All(answer.Sources, source => Assert.Equal(documentId, source.DocumentId));
                Assert.All(answer.Sources, source => Assert.Equal("manual.pdf", source.FileName));
                Assert.Equal(new string('a', 150) + "...", answer.Sources[0].Excerpt);
            });
    }

    [Fact]
    public async Task StartConversationAsyncLongQuestionTruncatesTitle()
    {
        var userId = await factory.CreateUserAsync();
        var question = new string('q', Conversation.MaxTitleLength + 50);

        var exchange = await this.UseServiceAsync(service => service.StartConversationAsync(userId, question));

        Assert.Equal(Conversation.MaxTitleLength, exchange.Conversation.Title.Length);
        Assert.EndsWith("…", exchange.Conversation.Title, StringComparison.Ordinal);
        Assert.Equal(question, exchange.Question.Content);
    }

    [Fact]
    public async Task StartConversationAsyncQueryFailureStoresNothing()
    {
        var userId = await factory.CreateUserAsync();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => this.UseServiceAsync(
            service => service.StartConversationAsync(userId, "What is DocMind?"),
            new FakeQueryService(fail: true)));

        await using var assertContext = factory.CreateDbContext();
        Assert.False(await assertContext.Conversations.AnyAsync(conversation => conversation.UserId == userId));
    }

    [Fact]
    public async Task AddMessageAsyncAppendsExchangeAndUpdatesConversation()
    {
        var userId = await factory.CreateUserAsync();
        var started = await this.UseServiceAsync(service => service.StartConversationAsync(userId, "First question"));

        var added = await this.UseServiceAsync(service => service.AddMessageAsync(userId, started.Conversation.Id, "Second question"));

        Assert.NotNull(added);
        Assert.Equal(started.Conversation.Id, added.Conversation.Id);
        Assert.Equal("First question", added.Conversation.Title);
        Assert.True(added.Conversation.UpdatedAt > started.Conversation.UpdatedAt);

        var conversation = await this.UseServiceAsync(service => service.GetConversationAsync(userId, started.Conversation.Id));
        Assert.NotNull(conversation);
        Assert.Equal(
            ["First question", Answer, "Second question", Answer],
            conversation.Messages.Select(message => message.Content));
    }

    [Fact]
    public async Task AddMessageAsyncConversationOfAnotherUserReturnsNullAndStoresNothing()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var started = await this.UseServiceAsync(service => service.StartConversationAsync(ownerId, "Owner's question"));

        var added = await this.UseServiceAsync(service => service.AddMessageAsync(otherUserId, started.Conversation.Id, "Intruder's question"));

        Assert.Null(added);
        await using var assertContext = factory.CreateDbContext();
        Assert.Equal(2, await assertContext.Messages.CountAsync(message => message.ConversationId == started.Conversation.Id));
    }

    [Fact]
    public async Task GetConversationAsyncConversationOfAnotherUserReturnsNull()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var started = await this.UseServiceAsync(service => service.StartConversationAsync(ownerId, "Owner's question"));

        var conversation = await this.UseServiceAsync(service => service.GetConversationAsync(otherUserId, started.Conversation.Id));

        Assert.Null(conversation);
    }

    [Fact]
    public async Task ListConversationsAsyncReturnsOwnConversationsMostRecentlyUsedFirst()
    {
        var userId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var older = await this.UseServiceAsync(service => service.StartConversationAsync(userId, "Older"));
        _ = await this.UseServiceAsync(service => service.StartConversationAsync(userId, "Newer"));
        _ = await this.UseServiceAsync(service => service.StartConversationAsync(otherUserId, "Someone else's"));

        // A new exchange in the older conversation moves it back to the top.
        _ = await this.UseServiceAsync(service => service.AddMessageAsync(userId, older.Conversation.Id, "Follow-up"));

        var conversations = await this.UseServiceAsync(service => service.ListConversationsAsync(userId));

        Assert.Equal(["Older", "Newer"], conversations.Select(conversation => conversation.Title));
    }

    [Fact]
    public async Task DeleteConversationAsyncRemovesMessagesAndSourcesButKeepsDocument()
    {
        var userId = await factory.CreateUserAsync();
        var documentId = await factory.SeedDocumentAsync(userId);
        var exchange = await this.UseServiceAsync(
            service => service.StartConversationAsync(userId, "What is DocMind?"),
            new FakeQueryService([SourceFrom(documentId)]));

        var deleted = await this.UseServiceAsync(service => service.DeleteConversationAsync(userId, exchange.Conversation.Id));

        Assert.True(deleted);
        await using var assertContext = factory.CreateDbContext();
        Assert.False(await assertContext.Conversations.AnyAsync(conversation => conversation.Id == exchange.Conversation.Id));
        Assert.False(await assertContext.Messages.AnyAsync(message => message.ConversationId == exchange.Conversation.Id));
        Assert.False(await assertContext.MessageSources.AnyAsync(source => source.MessageId == exchange.Answer.Id));
        Assert.True(await assertContext.Documents.AnyAsync(document => document.Id == documentId));
    }

    [Fact]
    public async Task DeleteConversationAsyncConversationOfAnotherUserReturnsFalseAndKeepsIt()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var started = await this.UseServiceAsync(service => service.StartConversationAsync(ownerId, "Owner's question"));

        var deleted = await this.UseServiceAsync(service => service.DeleteConversationAsync(otherUserId, started.Conversation.Id));

        Assert.False(deleted);
        await using var assertContext = factory.CreateDbContext();
        Assert.True(await assertContext.Conversations.AnyAsync(conversation => conversation.Id == started.Conversation.Id));
    }

    [Fact]
    public async Task DeletingCitedDocumentKeepsSourceWithoutDocumentId()
    {
        var userId = await factory.CreateUserAsync();
        var documentId = await factory.SeedDocumentAsync(userId, "manual.pdf");
        var exchange = await this.UseServiceAsync(
            service => service.StartConversationAsync(userId, "What is DocMind?"),
            new FakeQueryService([SourceFrom(documentId)]));

        // The same single DELETE that DocumentService.DeleteDocumentAsync runs.
        await using (var dbContext = factory.CreateDbContext())
        {
            _ = await dbContext.Documents.Where(document => document.Id == documentId).ExecuteDeleteAsync();
        }

        var conversation = await this.UseServiceAsync(service => service.GetConversationAsync(userId, exchange.Conversation.Id));
        Assert.NotNull(conversation);
        var source = Assert.Single(conversation.Messages[1].Sources);
        Assert.Null(source.DocumentId);
        Assert.Equal("manual.pdf", source.FileName);
    }

    private static ScoredChunk SourceFrom(
        Guid documentId,
        int sequenceNumber = 0,
        float score = 0.8f,
        string content = "DocMind is a RAG system.") =>
        new(new Chunk(Guid.NewGuid(), documentId, content, 5, sequenceNumber), score);

    // Each call gets its own DbContext, the way each request gets its own scope in the API, so what
    // a test reads back comes from the database and not from entities still tracked by the context
    // that wrote them.
    private async Task<T> UseServiceAsync<T>(Func<ConversationService, Task<T>> action, IQueryService? queryService = null)
    {
        await using var dbContext = factory.CreateDbContext();
        return await action(new ConversationService(queryService ?? new FakeQueryService(), dbContext));
    }

    // Stands in for the RAG pipeline: returns a fixed answer with the given sources, or fails the
    // way QueryService does when Ollama can't be reached.
    private sealed class FakeQueryService(List<ScoredChunk>? sources = null, bool fail = false) : IQueryService
    {
        public Task<QueryResult> AskAsync(Guid userId, string question, int topK = 5) =>
            fail
                ? throw new InvalidOperationException("Could not reach Ollama.")
                : Task.FromResult(new QueryResult(Answer, sources ?? []));
    }
}
