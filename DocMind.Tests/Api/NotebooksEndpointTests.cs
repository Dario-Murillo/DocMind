namespace DocMind.Tests.Api;

using System.Net;
using System.Net.Http.Json;
using DocMind.Api.Contracts;
using Microsoft.EntityFrameworkCore;

// The HTTP side of notebooks: routes, status codes and JSON shapes. What ends up in the database is
// covered by NotebookServiceTests.
[Collection(ApiCollectionDefinition.Name)]
public class NotebooksEndpointTests(DocMindApiFactory factory)
{
    [Fact]
    public async Task ListNotebooksWithoutSessionReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/notebooks");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateNotebookReturnsCreatedWithLocation()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/notebooks", new NotebookRequest("Thesis research"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var notebook = await response.Content.ReadFromJsonAsync<NotebookResponse>();
        Assert.NotNull(notebook);
        Assert.Equal($"/notebooks/{notebook.NotebookId}", response.Headers.Location?.ToString());
        Assert.Equal("Thesis research", notebook.Title);
        Assert.Equal(0, notebook.DocumentCount);
    }

    [Fact]
    public async Task CreateNotebookEmptyTitleReturnsBadRequest()
    {
        using var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/notebooks", new NotebookRequest("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Message));
    }

    [Fact]
    public async Task ListNotebooksReturnsOwnNotebooksWithDocumentCount()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var otherUserId = await factory.CreateUserAsync();
            var notebookId = await factory.SeedNotebookAsync(userId, "Mine");
            _ = await factory.SeedNotebookAsync(otherUserId, "Someone else's");
            _ = await factory.SeedDocumentAsync(userId, notebookId: notebookId);

            var notebooks = await client.GetFromJsonAsync<List<NotebookResponse>>("/notebooks");

            Assert.NotNull(notebooks);
            var notebook = Assert.Single(notebooks);
            Assert.Equal(notebookId, notebook.NotebookId);
            Assert.Equal("Mine", notebook.Title);
            Assert.Equal(1, notebook.DocumentCount);
        }
    }

    [Fact]
    public async Task RenameNotebookReturnsRenamedNotebook()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var notebookId = await factory.SeedNotebookAsync(userId, "Old title");

            var response = await client.PatchAsJsonAsync($"/notebooks/{notebookId}", new NotebookRequest("New title"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var notebook = await client.GetFromJsonAsync<NotebookResponse>($"/notebooks/{notebookId}");
            Assert.Equal("New title", notebook?.Title);
        }
    }

    [Fact]
    public async Task DeleteNotebookReturnsNoContentAndRemovesIt()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var notebookId = await factory.SeedNotebookAsync(userId);

            var deleted = await client.DeleteAsync($"/notebooks/{notebookId}");
            var fetched = await client.GetAsync($"/notebooks/{notebookId}");

            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
        }
    }

    // Another user's notebook must look exactly like a missing one, on every route under it.
    [Theory]
    [InlineData("GET", "")]
    [InlineData("PATCH", "")]
    [InlineData("DELETE", "")]
    [InlineData("GET", "/documents")]
    [InlineData("GET", "/conversations")]
    public async Task NotebookOfAnotherUserReturnsNotFoundAndKeepsIt(string method, string pathSuffix)
    {
        var ownerId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(ownerId, "Original");
        using var otherClient = await factory.CreateAuthenticatedClientAsync();

        using var request = new HttpRequestMessage(new HttpMethod(method), $"/notebooks/{notebookId}{pathSuffix}")
        {
            Content = method == "PATCH" ? JsonContent.Create(new NotebookRequest("Taken over")) : null,
        };
        var response = await otherClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var dbContext = factory.CreateDbContext();
        Assert.Equal("Original", await dbContext.Notebooks.Where(notebook => notebook.Id == notebookId).Select(notebook => notebook.Title).SingleAsync());
    }
}
