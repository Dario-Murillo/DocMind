namespace DocMind.Core.Documents;

using System.Security.Cryptography;
using System.Text;
using DocMind.Core.Chunking;
using DocMind.Core.Data;
using DocMind.Core.Embeddings;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using UglyToad.PdfPig;

public class DocumentService(
    IChunkingService chunkingService,
    IEmbeddingService embeddingService,
    DocMindDbContext dbContext) : IDocumentService
{
    // PDFs are stored in Postgres (bytea), so their size is capped to keep rows and backups reasonable.
    public const int MaxFileSizeBytes = 25 * 1024 * 1024;

    private readonly IChunkingService chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
    private readonly IEmbeddingService embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
    private readonly DocMindDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<Guid> IndexDocumentAsync(Guid userId, Stream pdfStream, string fileName)
    {
        if (pdfStream is null)
        {
            throw new ArgumentException("PDF stream must not be null.", nameof(pdfStream));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must not be null or empty.", nameof(fileName));
        }

        var content = await ReadAllBytesAsync(pdfStream);
        if (content.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException($"'{fileName}' exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB upload limit.", nameof(pdfStream));
        }

        // Checked before extracting text or generating embeddings, so a re-upload costs one query
        // instead of a full round of Ollama calls.
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        var existingFileName = await this.dbContext.Documents
            .Where(document => document.UserId == userId && document.Sha256 == sha256)
            .Select(document => document.FileName)
            .FirstOrDefaultAsync();
        if (existingFileName is not null)
        {
            throw new DuplicateDocumentException($"'{fileName}' has already been uploaded as '{existingFileName}'.");
        }

        var text = ExtractText(content);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new NoExtractableTextException(
                $"No extractable text found in '{fileName}'. The PDF may be empty or a scanned document without OCR.");
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = fileName,
            SizeBytes = content.Length,
            Sha256 = sha256,
            EmbeddingModel = this.embeddingService.ModelId,
            CreatedAt = DateTimeOffset.UtcNow,
            File = new DocumentFile { Content = content },
        };

        // Every embedding is generated before anything is written, so an Ollama failure halfway
        // through leaves the database untouched.
        foreach (var chunk in this.chunkingService.ChunkText(text, document.Id))
        {
            var vector = await this.embeddingService.GenerateEmbeddingAsync(chunk.Content);
            document.Chunks.Add(new DocumentChunk
            {
                Id = chunk.Id,
                UserId = userId,
                SequenceNumber = chunk.SequenceNumber,
                Content = chunk.Content,
                TokenCount = chunk.TokenCount,
                Embedding = new Vector(vector),
            });
        }

        // One SaveChangesAsync inserts the document, its file and all of its chunks in a single
        // transaction: the document is stored complete or not at all.
        _ = this.dbContext.Documents.Add(document);
        _ = await this.dbContext.SaveChangesAsync();

        return document.Id;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private static string ExtractText(byte[] pdfContent)
    {
        using var document = PdfDocument.Open(pdfContent);

        var textBuilder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            _ = textBuilder.AppendLine(page.Text);
        }

        return textBuilder.ToString();
    }
}
