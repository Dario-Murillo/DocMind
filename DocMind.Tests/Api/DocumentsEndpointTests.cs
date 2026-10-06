namespace DocMind.Tests.Api;

using System.Net;
using System.Net.Http.Json;
using DocMind.Api.Contracts;
using Microsoft.EntityFrameworkCore;

[Collection(ApiCollectionDefinition.Name)]
public class DocumentsEndpointTests(DocMindApiFactory factory)
{
    [Fact]
    public async Task ListDocumentsWithoutSessionReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/documents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListDocumentsReturnsOwnDocumentsNewestFirst()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var otherUserId = await factory.CreateUserAsync();
            _ = await factory.SeedDocumentAsync(userId, "first.pdf");
            _ = await factory.SeedDocumentAsync(userId, "second.pdf");
            _ = await factory.SeedDocumentAsync(otherUserId, "other.pdf");

            var documents = await client.GetFromJsonAsync<List<DocumentResponse>>("/documents");

            Assert.NotNull(documents);
            Assert.Equal(["second.pdf", "first.pdf"], documents.Select(document => document.FileName));
        }
    }

    [Fact]
    public async Task DownloadFileReturnsOriginalPdf()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0x2D];
            var documentId = await factory.SeedDocumentAsync(userId, "report.pdf", pdf);

            var response = await client.GetAsync($"/documents/{documentId}/file");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("report.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
            Assert.Equal(pdf, await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task DownloadFileOfAnotherUserReturnsNotFound()
    {
        var ownerId = await factory.CreateUserAsync();
        var documentId = await factory.SeedDocumentAsync(ownerId);
        using var otherClient = await factory.CreateAuthenticatedClientAsync();

        var response = await otherClient.GetAsync($"/documents/{documentId}/file");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDocumentRemovesDocumentFileAndChunks()
    {
        var (client, userId) = await factory.CreateAuthenticatedUserAsync();
        using (client)
        {
            var documentId = await factory.SeedDocumentAsync(userId);

            var response = await client.DeleteAsync($"/documents/{documentId}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            await using var dbContext = factory.CreateDbContext();
            Assert.False(await dbContext.Documents.AnyAsync(document => document.Id == documentId));
            Assert.False(await dbContext.DocumentFiles.AnyAsync(file => file.DocumentId == documentId));
            Assert.False(await dbContext.DocumentChunks.AnyAsync(chunk => chunk.DocumentId == documentId));
        }
    }

    [Fact]
    public async Task DeleteDocumentOfAnotherUserReturnsNotFoundAndKeepsIt()
    {
        var ownerId = await factory.CreateUserAsync();
        var documentId = await factory.SeedDocumentAsync(ownerId);
        using var otherClient = await factory.CreateAuthenticatedClientAsync();

        var response = await otherClient.DeleteAsync($"/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var dbContext = factory.CreateDbContext();
        Assert.True(await dbContext.Documents.AnyAsync(document => document.Id == documentId));
    }
}
