namespace DocMind.Tests.Api;

using System.Net;
using System.Net.Http.Json;
using DocMind.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

public class QueryEndpointTests
{
    [Fact]
    public async Task QueryEmptyQuestionReturnsBadRequest()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/query", new QueryRequest(string.Empty, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Message));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task QueryEmptyVectorStoreReturnsOkWithNoSources()
    {
        // Requires a local Ollama instance running with nomic-embed-text and llama3.1 pulled:
        // an empty vector store doesn't skip the embedding/completion calls, it just means
        // CompletionService gets no context chunks and answers accordingly.
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/query", new QueryRequest("What is DocMind?", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<QueryResponse>();
        Assert.NotNull(body);
        Assert.Empty(body.Sources);
        Assert.False(string.IsNullOrWhiteSpace(body.Answer));
    }
}
