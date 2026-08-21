namespace DocMind.Tests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DocMind.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

public class UploadEndpointTests
{
    [Fact]
    public async Task UploadDocumentNoFileReturnsBadRequest()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/documents/upload", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Message));
    }

    [Fact]
    public async Task UploadDocumentNonPdfFileReturnsBadRequest()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("just some plain text, not a PDF"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "notes.txt");

        var response = await client.PostAsync("/documents/upload", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains(".pdf", body?.Message, StringComparison.OrdinalIgnoreCase);
    }
}
