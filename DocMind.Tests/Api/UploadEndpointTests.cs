namespace DocMind.Tests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DocMind.Api.Contracts;

[Collection(ApiCollectionDefinition.Name)]
public class UploadEndpointTests(DocMindApiFactory factory)
{
    [Fact]
    public async Task UploadDocumentNoFileReturnsBadRequest()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var notebookId = await factory.SeedNotebookAsync(userId);

            var response = await client.PostAsync($"/notebooks/{notebookId}/documents", content: null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.False(string.IsNullOrWhiteSpace(body?.Message));
        }
    }

    [Fact]
    public async Task UploadDocumentNonPdfFileReturnsBadRequest()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var notebookId = await factory.SeedNotebookAsync(userId);
            using var form = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("just some plain text, not a PDF"));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(fileContent, "file", "notes.txt");

            var response = await client.PostAsync($"/notebooks/{notebookId}/documents", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.Contains(".pdf", body?.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task UploadDocumentToNotebookOfAnotherUserReturnsNotFound()
    {
        var ownerId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(ownerId);
        using var otherClient = await factory.CreateAuthenticatedClientAsync();
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]);
        form.Add(fileContent, "file", "report.pdf");

        var response = await otherClient.PostAsync($"/notebooks/{notebookId}/documents", form);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
