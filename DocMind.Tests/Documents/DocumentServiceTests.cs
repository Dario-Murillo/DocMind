namespace DocMind.Tests.Documents;


using System.Text;
using DocMind.Core.Chunking;
using DocMind.Core.Documents;
using DocMind.Core.Embeddings;
using DocMind.Core.VectorStore;

public class DocumentServiceTests
{
    [Fact]
    public async Task IndexDocumentAsyncNullPdfStreamThrowsArgumentException()
    {
        var service = CreateService(out _, out _, out _);

        await Assert.ThrowsAsync<ArgumentException>(() => service.IndexDocumentAsync(null!, "file.pdf"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IndexDocumentAsyncNullOrEmptyFileNameThrowsArgumentException(string? fileName)
    {
        var service = CreateService(out _, out _, out _);
        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(() => service.IndexDocumentAsync(stream, fileName!));
    }

    [Fact]
    public async Task IndexDocumentAsyncPdfWithoutExtractableTextThrowsInvalidOperationException()
    {
        var service = CreateService(out _, out _, out _);
        using var stream = new MemoryStream(BuildMinimalPdf(content: string.Empty));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.IndexDocumentAsync(stream, "empty.pdf"));
    }

    [Fact]
    public async Task IndexDocumentAsyncValidPdfCallsChunkingThenEmbedsAndStoresEachChunkInOrder()
    {
        var callLog = new List<string>();
        var chunk1 = new Chunk(Guid.NewGuid(), "placeholder", "chunk one content", TokenCount: 3, SequenceNumber: 0);
        var chunk2 = new Chunk(Guid.NewGuid(), "placeholder", "chunk two content", TokenCount: 3, SequenceNumber: 1);

        var service = CreateService(out var chunkingService, out var embeddingService, out var vectorStoreService, callLog, [chunk1, chunk2]);
        using var stream = new MemoryStream(BuildMinimalPdf("Hello World from DocMind"));

        var documentId = await service.IndexDocumentAsync(stream, "hello.pdf");

        Assert.False(string.IsNullOrWhiteSpace(documentId));
        Assert.Contains("Hello World from DocMind", chunkingService.ReceivedText);
        Assert.Equal(documentId, chunkingService.ReceivedDocumentId);

        Assert.Equal(
            [
                "Chunk",
                "Embed:chunk one content",
                "Add:chunk one content",
                "Embed:chunk two content",
                "Add:chunk two content",
            ],
            callLog);

        Assert.Equal(2, vectorStoreService.AddedEntries.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IndexPlainTextAsyncNullOrEmptyTextThrowsArgumentException(string? text)
    {
        var service = CreateService(out _, out _, out _);

        await Assert.ThrowsAsync<ArgumentException>(() => service.IndexPlainTextAsync(text!, "doc.txt"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IndexPlainTextAsyncNullOrEmptyDocumentNameThrowsArgumentException(string? documentName)
    {
        var service = CreateService(out _, out _, out _);

        await Assert.ThrowsAsync<ArgumentException>(() => service.IndexPlainTextAsync("some text", documentName!));
    }

    [Fact]
    public async Task IndexPlainTextAsyncValidTextCallsChunkingThenEmbedsAndStoresEachChunkInOrder()
    {
        var callLog = new List<string>();
        var chunk1 = new Chunk(Guid.NewGuid(), "placeholder", "chunk one content", TokenCount: 3, SequenceNumber: 0);
        var chunk2 = new Chunk(Guid.NewGuid(), "placeholder", "chunk two content", TokenCount: 3, SequenceNumber: 1);

        var service = CreateService(out var chunkingService, out var embeddingService, out var vectorStoreService, callLog, [chunk1, chunk2]);

        var documentId = await service.IndexPlainTextAsync("Hello World from DocMind", "hello.txt");

        Assert.False(string.IsNullOrWhiteSpace(documentId));
        Assert.Equal("Hello World from DocMind", chunkingService.ReceivedText);
        Assert.Equal(documentId, chunkingService.ReceivedDocumentId);

        Assert.Equal(
            [
                "Chunk",
                "Embed:chunk one content",
                "Add:chunk one content",
                "Embed:chunk two content",
                "Add:chunk two content",
            ],
            callLog);

        Assert.Equal(2, vectorStoreService.AddedEntries.Count);
    }

    private static DocumentService CreateService(
        out FakeChunkingService chunkingService,
        out FakeEmbeddingService embeddingService,
        out FakeVectorStoreService vectorStoreService,
        List<string>? callLog = null,
        List<Chunk>? chunksToReturn = null)
    {
        callLog ??= [];
        chunkingService = new FakeChunkingService(callLog, chunksToReturn ?? []);
        embeddingService = new FakeEmbeddingService(callLog);
        vectorStoreService = new FakeVectorStoreService(callLog);

        return new DocumentService(chunkingService, embeddingService, vectorStoreService);
    }

    // Hand-built minimal single-page PDF (no external PDF-generation library available):
    // header, five direct objects (Catalog, Pages, Page, Font, content stream) and a
    // byte-accurate xref/trailer, verified against a real PdfPig parse.
    private static byte[] BuildMinimalPdf(string content)
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

        WriteAscii($"trailer\n<< /Size {objectCount} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

        return ms.ToArray();
    }

    private sealed class FakeChunkingService(List<string> callLog, List<Chunk> chunksToReturn) : IChunkingService
    {
        public string? ReceivedText { get; private set; }
        public string? ReceivedDocumentId { get; private set; }

        public List<Chunk> ChunkText(string text, string sourceDocumentId)
        {
            this.ReceivedText = text;
            this.ReceivedDocumentId = sourceDocumentId;
            callLog.Add("Chunk");
            return chunksToReturn;
        }
    }

    private sealed class FakeEmbeddingService(List<string> callLog) : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            callLog.Add($"Embed:{text}");
            return Task.FromResult<float[]>([1f, 0f]);
        }
    }

    private sealed class FakeVectorStoreService(List<string> callLog) : IVectorStoreService
    {
        public List<(Chunk Chunk, float[] Vector)> AddedEntries { get; } = [];

        public void Add(Chunk chunk, float[] vector)
        {
            callLog.Add($"Add:{chunk.Content}");
            this.AddedEntries.Add((chunk, vector));
        }

        public List<ScoredChunk> Search(float[] queryVector, int topK = 5) =>
            throw new NotSupportedException("Not used by DocumentService tests.");
    }
}
