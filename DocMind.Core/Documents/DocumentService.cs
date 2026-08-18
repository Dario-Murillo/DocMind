using System.Text;
using DocMind.Core.Chunking;
using DocMind.Core.Embeddings;
using DocMind.Core.VectorStore;
using UglyToad.PdfPig;

namespace DocMind.Core.Documents;

public class DocumentService(
    IChunkingService chunkingService,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService) : IDocumentService
{
    private readonly IChunkingService _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
    private readonly IEmbeddingService _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
    private readonly IVectorStoreService _vectorStoreService = vectorStoreService ?? throw new ArgumentNullException(nameof(vectorStoreService));

    public async Task<string> IndexDocumentAsync(Stream pdfStream, string fileName)
    {
        if (pdfStream is null)
            throw new ArgumentException("PDF stream must not be null.", nameof(pdfStream));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name must not be null or empty.", nameof(fileName));

        var text = ExtractText(pdfStream);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                $"No extractable text found in '{fileName}'. The PDF may be empty or a scanned document without OCR.");
        }

        return await IndexTextAsync(text);
    }

    public async Task<string> IndexPlainTextAsync(string text, string documentName)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be null or empty.", nameof(text));
        if (string.IsNullOrWhiteSpace(documentName))
            throw new ArgumentException("Document name must not be null or empty.", nameof(documentName));

        return await IndexTextAsync(text);
    }

    private async Task<string> IndexTextAsync(string text)
    {
        var documentId = Guid.NewGuid().ToString();
        var chunks = _chunkingService.ChunkText(text, documentId);

        foreach (var chunk in chunks)
        {
            var vector = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);
            _vectorStoreService.Add(chunk, vector);
        }

        return documentId;
    }

    private static string ExtractText(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);

        var textBuilder = new StringBuilder();
        foreach (var page in document.GetPages())
            textBuilder.AppendLine(page.Text);

        return textBuilder.ToString();
    }
}
