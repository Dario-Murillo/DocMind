namespace DocMind.Tests.Documents;

using System.Text;
using DocMind.Core.Chunking;
using DocMind.Core.Data;
using DocMind.Core.Documents;
using DocMind.Core.Embeddings;
using DocMind.Core.Notebooks;
using DocMind.Tests.Api;
using Microsoft.EntityFrameworkCore;

// Runs against the Postgres container shared by the Api collection: what matters here is what ends
// up in the database (the vector column, the unique index, the single transaction), which a fake
// store can't reproduce.
[Collection(ApiCollectionDefinition.Name)]
public class DocumentServiceTests(DocMindApiFactory factory)
{
    [Fact]
    public async Task IndexDocumentAsyncNullPdfStreamThrowsArgumentException()
    {
        await using var dbContext = factory.CreateDbContext();
        var service = CreateService(dbContext);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => service.IndexDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), null!, "file.pdf"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IndexDocumentAsyncNullOrEmptyFileNameThrowsArgumentException(string? fileName)
    {
        await using var dbContext = factory.CreateDbContext();
        var service = CreateService(dbContext);
        using var stream = new MemoryStream([1, 2, 3]);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => service.IndexDocumentAsync(Guid.NewGuid(), Guid.NewGuid(), stream, fileName!));
    }

    [Fact]
    public async Task IndexDocumentAsyncFileAboveLimitThrowsArgumentException()
    {
        var userId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(userId);
        await using var dbContext = factory.CreateDbContext();
        var service = CreateService(dbContext);
        using var stream = new MemoryStream(new byte[DocumentService.MaxFileSizeBytes + 1]);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => service.IndexDocumentAsync(userId, notebookId, stream, "huge.pdf"));
    }

    [Fact]
    public async Task IndexDocumentAsyncPdfWithoutExtractableTextThrowsNoExtractableTextException()
    {
        var userId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(userId);

        _ = await Assert.ThrowsAsync<NoExtractableTextException>(() =>
            this.IndexAsync(userId, notebookId, BuildMinimalPdf(content: string.Empty), "empty.pdf"));
    }

    [Fact]
    public async Task IndexDocumentAsyncFileThatIsNotAPdfThrowsNoExtractableTextException()
    {
        var userId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(userId);

        var ex = await Assert.ThrowsAsync<NoExtractableTextException>(() =>
            this.IndexAsync(userId, notebookId, Encoding.ASCII.GetBytes("not a pdf"), "renamed.pdf"));

        Assert.Contains("could not be read as a PDF", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndexDocumentAsyncPasswordProtectedPdfThrowsNoExtractableTextException()
    {
        var userId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(userId);
        var pdf = BuildMinimalPdf("Secret", PasswordProtection);

        var ex = await Assert.ThrowsAsync<NoExtractableTextException>(() =>
            this.IndexAsync(userId, notebookId, pdf, "locked.pdf"));

        Assert.Contains("password-protected", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndexDocumentAsyncValidPdfStoresDocumentFileAndChunks()
    {
        var userId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(userId);
        var pdf = BuildMinimalPdf("Hello World from DocMind");

        var documentId = await this.IndexAsync(userId, notebookId, pdf, "hello.pdf");

        await using var assertContext = factory.CreateDbContext();
        var document = await assertContext.Documents
            .Include(document => document.File)
            .Include(document => document.Chunks)
            .SingleAsync(document => document.Id == documentId);

        Assert.Equal(userId, document.UserId);
        Assert.Equal(notebookId, document.NotebookId);
        Assert.Equal("hello.pdf", document.FileName);
        Assert.Equal(pdf.Length, document.SizeBytes);
        Assert.Equal(64, document.Sha256.Length);
        Assert.Equal(FakeEmbeddingService.FakeModelId, document.EmbeddingModel);
        Assert.Equal(pdf, document.File?.Content);
        Assert.NotEmpty(document.Chunks);
        Assert.All(document.Chunks, chunk => Assert.Equal(userId, chunk.UserId));
        Assert.All(document.Chunks, chunk => Assert.Equal(notebookId, chunk.NotebookId));
        Assert.Equal(
            Enumerable.Range(0, document.Chunks.Count),
            document.Chunks.Select(chunk => chunk.SequenceNumber).Order());
    }

    [Fact]
    public async Task IndexDocumentAsyncSameFileTwiceThrowsDuplicateDocumentException()
    {
        var userId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(userId);
        var pdf = BuildMinimalPdf("Hello World from DocMind");
        _ = await this.IndexAsync(userId, notebookId, pdf, "hello.pdf");

        var exception = await Assert.ThrowsAsync<DuplicateDocumentException>(() => this.IndexAsync(userId, notebookId, pdf, "copy.pdf"));

        Assert.Contains("hello.pdf", exception.Message, StringComparison.Ordinal);
        await using var assertContext = factory.CreateDbContext();
        Assert.Equal(1, await assertContext.Documents.CountAsync(document => document.UserId == userId));
    }

    [Fact]
    public async Task IndexDocumentAsyncSameFileInTwoNotebooksStoresBoth()
    {
        var userId = await factory.CreateUserAsync();
        var firstNotebookId = await factory.SeedNotebookAsync(userId);
        var secondNotebookId = await factory.SeedNotebookAsync(userId);
        var pdf = BuildMinimalPdf("Hello World from DocMind");

        var firstDocumentId = await this.IndexAsync(userId, firstNotebookId, pdf, "hello.pdf");
        var secondDocumentId = await this.IndexAsync(userId, secondNotebookId, pdf, "hello.pdf");

        Assert.NotEqual(firstDocumentId, secondDocumentId);
    }

    [Fact]
    public async Task IndexDocumentAsyncNotebookOfAnotherUserThrowsNotebookNotFoundException()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(ownerId);

        _ = await Assert.ThrowsAsync<NotebookNotFoundException>(() =>
            this.IndexAsync(otherUserId, notebookId, BuildMinimalPdf("Hello World from DocMind"), "hello.pdf"));

        await using var assertContext = factory.CreateDbContext();
        Assert.False(await assertContext.Documents.AnyAsync(document => document.NotebookId == notebookId));
    }

    [Fact]
    public async Task ListDocumentsAsyncNotebookOfAnotherUserThrowsNotebookNotFoundException()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(ownerId);
        await using var dbContext = factory.CreateDbContext();
        var service = CreateService(dbContext);

        _ = await Assert.ThrowsAsync<NotebookNotFoundException>(() => service.ListDocumentsAsync(otherUserId, notebookId));
    }

    [Fact]
    public async Task IndexDocumentAsyncSameFileDifferentUsersStoresBoth()
    {
        var firstUserId = await factory.CreateUserAsync();
        var secondUserId = await factory.CreateUserAsync();
        var firstNotebookId = await factory.SeedNotebookAsync(firstUserId);
        var secondNotebookId = await factory.SeedNotebookAsync(secondUserId);
        var pdf = BuildMinimalPdf("Hello World from DocMind");

        var firstDocumentId = await this.IndexAsync(firstUserId, firstNotebookId, pdf, "hello.pdf");
        var secondDocumentId = await this.IndexAsync(secondUserId, secondNotebookId, pdf, "hello.pdf");

        Assert.NotEqual(firstDocumentId, secondDocumentId);
    }

    [Fact]
    public async Task IndexDocumentAsyncEmbeddingFailureStoresNothing()
    {
        var userId = await factory.CreateUserAsync();
        var notebookId = await factory.SeedNotebookAsync(userId);

        // Small chunks so the document yields several of them and the failure happens after at
        // least one embedding has already succeeded.
        var chunkingService = new ChunkingService(chunkSizeTokens: 10, overlapTokens: 2, minChunkTokens: 1);
        var pdf = BuildMinimalPdf(string.Join(' ', Enumerable.Repeat("DocMind", 30)));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            this.IndexAsync(userId, notebookId, pdf, "hello.pdf", new FakeEmbeddingService(failOnCall: 2), chunkingService));

        await using var assertContext = factory.CreateDbContext();
        Assert.False(await assertContext.Documents.AnyAsync(document => document.UserId == userId));
        Assert.False(await assertContext.DocumentChunks.AnyAsync(chunk => chunk.UserId == userId));
    }

    // A Standard security handler whose password hashes match no password, so PdfPig can't open the
    // file without one: what a password-protected PDF looks like to DocMind.
    private static readonly string PasswordProtection =
        $"/Encrypt << /Filter /Standard /V 1 /R 2 /O <{new string('A', 64)}> /U <{new string('A', 64)}> /P -4 >> /ID [<{new string('A', 64)}> <{new string('A', 64)}>] ";

    private static DocumentService CreateService(
        DocMindDbContext dbContext,
        IEmbeddingService? embeddingService = null,
        IChunkingService? chunkingService = null) =>
        new(chunkingService ?? new ChunkingService(), embeddingService ?? new FakeEmbeddingService(), dbContext);

    // Hand-built minimal single-page PDF (no external PDF-generation library available):
    // header, five direct objects (Catalog, Pages, Page, Font, content stream) and a
    // byte-accurate xref/trailer, verified against a real PdfPig parse.
    private static byte[] BuildMinimalPdf(string content, string trailerEntries = "")
    {
        var obj1 = "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n";
        var obj2 = "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n";
        var obj3 = "3 0 obj\n<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /MediaBox [0 0 300 200] /Contents 5 0 R >>\nendobj\n";
        var obj4 = "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n";

        var streamBody = content.Length == 0 ? string.Empty : $"BT /F1 24 Tf 20 100 Td ({content}) Tj ET";
        var obj5 = $"5 0 obj\n<< /Length {streamBody.Length} >>\nstream\n{streamBody}\nendstream\nendobj\n";

        var parts = new List<string> { obj1, obj2, obj3, obj4, obj5 };

        using var ms = new MemoryStream();
        void WriteAscii(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        WriteAscii("%PDF-1.4\n");

        var offsets = new long[parts.Count + 1];
        for (var i = 0; i < parts.Count; i++)
        {
            offsets[i + 1] = ms.Position;
            WriteAscii(parts[i]);
        }

        var xrefOffset = ms.Position;
        var objectCount = parts.Count + 1;
        WriteAscii($"xref\n0 {objectCount}\n");
        WriteAscii("0000000000 65535 f \n");
        for (var i = 1; i < objectCount; i++)
        {
            WriteAscii($"{offsets[i]:D10} 00000 n \n");
        }

        WriteAscii($"trailer\n<< /Size {objectCount} /Root 1 0 R {trailerEntries}>>\nstartxref\n{xrefOffset}\n%%EOF");

        return ms.ToArray();
    }

    // Each call gets its own DbContext, the way each upload request gets its own scope in the API.
    private async Task<Guid> IndexAsync(
        Guid userId,
        Guid notebookId,
        byte[] pdf,
        string fileName,
        IEmbeddingService? embeddingService = null,
        IChunkingService? chunkingService = null)
    {
        await using var dbContext = factory.CreateDbContext();
        using var stream = new MemoryStream(pdf);
        var service = CreateService(dbContext, embeddingService, chunkingService);

        return await service.IndexDocumentAsync(userId, notebookId, stream, fileName);
    }

    // Returns a fixed 768-dimensional vector (pgvector rejects any other size for this column),
    // optionally failing on the nth call to simulate Ollama going down halfway through a document.
    private sealed class FakeEmbeddingService(int? failOnCall = null) : IEmbeddingService
    {
        public const string FakeModelId = "fake-embedding-model";

        private int calls;

        public string ModelId => FakeModelId;

        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            this.calls++;
            if (this.calls == failOnCall)
            {
                throw new InvalidOperationException("Simulated Ollama failure.");
            }

            var vector = new float[DocumentChunk.EmbeddingDimensions];
            vector[0] = 1f;
            return Task.FromResult(vector);
        }
    }
}
