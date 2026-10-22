namespace DocMind.Tests.Api;

using System.Net;
using System.Net.Http.Json;
using DocMind.Api.Contracts;
using Microsoft.EntityFrameworkCore;

// The HTTP side of conversations: routes, status codes and JSON shapes. What ends up in the database
// is covered by ConversationServiceTests. Conversations are seeded directly, so only the test marked
// Integration needs Ollama.
[Collection(ApiCollectionDefinition.Name)]
public class ConversationsEndpointTests(DocMindApiFactory factory)
{
    [Fact]
    public async Task StartConversationEmptyQuestionReturnsBadRequest()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/conversations", new AskRequest("   ", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Message));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StartConversationReturnsCreatedWithQuestionAndAnswer()
    {
        // Requires a local Ollama instance running with nomic-embed-text and llama3.1 pulled. The
        // user has no documents, so the answer comes back without sources.
        using var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/conversations", new AskRequest("What is DocMind?", null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var exchange = await response.Content.ReadFromJsonAsync<ExchangeResponse>();
        Assert.NotNull(exchange);
        Assert.Equal($"/conversations/{exchange.Conversation.ConversationId}", response.Headers.Location?.ToString());
        Assert.Equal("What is DocMind?", exchange.Conversation.Title);
        Assert.Equal("user", exchange.Question.Role);
        Assert.Equal("assistant", exchange.Answer.Role);
        Assert.False(string.IsNullOrWhiteSpace(exchange.Answer.Content));
        Assert.Empty(exchange.Answer.Sources);

        var conversation = await client.GetFromJsonAsync<ConversationDetailResponse>(response.Headers.Location);
        Assert.Equal(2, conversation?.Messages.Count);
    }

    [Fact]
    public async Task ListConversationsReturnsOwnConversations()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var otherUserId = await factory.CreateUserAsync();
            var conversationId = await factory.SeedConversationAsync(userId, "Mine");
            _ = await factory.SeedConversationAsync(otherUserId, "Someone else's");

            var conversations = await client.GetFromJsonAsync<List<ConversationResponse>>("/conversations");

            Assert.NotNull(conversations);
            var conversation = Assert.Single(conversations);
            Assert.Equal(conversationId, conversation.ConversationId);
            Assert.Equal("Mine", conversation.Title);
        }
    }

    [Fact]
    public async Task GetConversationReturnsMessagesWithRolesAndSources()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var documentId = await factory.SeedDocumentAsync(userId, "manual.pdf");
            var conversationId = await factory.SeedConversationAsync(userId, "What is DocMind?", documentId);

            var conversation = await client.GetFromJsonAsync<ConversationDetailResponse>($"/conversations/{conversationId}");

            Assert.NotNull(conversation);
            Assert.Equal("What is DocMind?", conversation.Title);
            Assert.Equal(["user", "assistant"], conversation.Messages.Select(message => message.Role));
            Assert.Empty(conversation.Messages[0].Sources);
            var source = Assert.Single(conversation.Messages[1].Sources);
            Assert.Equal(documentId, source.DocumentId);
            Assert.Equal("manual.pdf", source.FileName);
        }
    }

    [Fact]
    public async Task DeleteConversationReturnsNoContentAndRemovesIt()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var conversationId = await factory.SeedConversationAsync(userId);

            var deleted = await client.DeleteAsync($"/conversations/{conversationId}");
            var fetched = await client.GetAsync($"/conversations/{conversationId}");

            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
        }
    }

    // Another user's conversation must look exactly like a missing one, whatever the request: 404,
    // and nothing is added to it or removed from it. Asking in it returns 404 before Ollama is
    // called, so this test doesn't need Ollama either.
    [Theory]
    [InlineData("GET", "")]
    [InlineData("DELETE", "")]
    [InlineData("POST", "/messages")]
    public async Task ConversationOfAnotherUserReturnsNotFoundAndKeepsIt(string method, string pathSuffix)
    {
        var ownerId = await factory.CreateUserAsync();
        var conversationId = await factory.SeedConversationAsync(ownerId);
        using var otherClient = await factory.CreateAuthenticatedClientAsync();

        using var request = new HttpRequestMessage(new HttpMethod(method), $"/conversations/{conversationId}{pathSuffix}")
        {
            Content = method == "POST" ? JsonContent.Create(new AskRequest("Intruder's question", null)) : null,
        };
        var response = await otherClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var dbContext = factory.CreateDbContext();
        Assert.Equal(2, await dbContext.Messages.CountAsync(message => message.ConversationId == conversationId));
    }
}
