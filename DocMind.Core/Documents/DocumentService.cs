namespace DocMind.Core.Documents;

using System.Security.Cryptography;
using System.Text;
using DocMind.Core.Chunking;
using DocMind.Core.Data;
using DocMind.Core.Embeddings;
using DocMind.Core.Notebooks;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

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

    public async Task<Guid> IndexDocumentAsync(Guid userId, Guid notebookId, Stream pdfStream, string fileName)
    {
        if (pdfStream is null)
        {
            throw new ArgumentException("PDF stream must not be null.", nameof(pdfStream));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must not be null or empty.", nameof(fileName));
        }

        await this.dbContext.EnsureNotebookExistsAsync(userId, notebookId);

        var content = await ReadAllBytesAsync(pdfStream);
        if (content.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException($"'{fileName}' exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB upload limit.", nameof(pdfStream));
        }

        // Checked before extracting text or generating embeddings, so a re-upload costs one query
        // instead of a full round of Ollama calls.
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        var existingFileName = await this.dbContext.Documents
            .Where(document => document.NotebookId == notebookId && document.UserId == userId && document.Sha256 == sha256)
            .Select(document => document.FileName)
            .FirstOrDefaultAsync();
        if (existingFileName is not null)
        {
            throw new DuplicateDocumentException($"'{fileName}' has already been uploaded as '{existingFileName}'.");
        }

        var text = ExtractText(content, fileName);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new NoExtractableTextException(
                $"No extractable text found in '{fileName}'. The PDF may be empty or a scanned document without OCR.");
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NotebookId = notebookId,
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
                NotebookId = notebookId,
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

    public async Task<List<DocumentSummary>> ListDocumentsAsync(Guid userId, Guid notebookId)
    {
        await this.dbContext.EnsureNotebookExistsAsync(userId, notebookId);

        return await this.dbContext.Documents
            .Where(document => document.NotebookId == notebookId && document.UserId == userId)
            .OrderByDescending(document => document.CreatedAt)
            .Select(document => new DocumentSummary(document.Id, document.FileName, document.SizeBytes, document.CreatedAt))
            .ToListAsync();
    }

    // Filtering by both id and owner means another user's document behaves exactly like a missing one.
    public Task<DocumentFileContent?> GetFileAsync(Guid userId, Guid documentId) =>
        this.dbContext.Documents
            .Where(document => document.Id == documentId && document.UserId == userId)
            .Select(document => new DocumentFileContent(document.FileName, document.File!.Content))
            .SingleOrDefaultAsync();

    // A single DELETE without loading the document first. Its file and chunks are removed by the
    // database's ON DELETE CASCADE, not by EF, which never sees them.
    public async Task<bool> DeleteDocumentAsync(Guid userId, Guid documentId) =>
        await this.dbContext.Documents
            .Where(document => document.Id == documentId && document.UserId == userId)
            .ExecuteDeleteAsync() > 0;

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    // PdfPig's exceptions derive straight from Exception, so without this a damaged or locked file
    // would reach the API as a 500. They're the file's fault, not the server's: 422, like a PDF
    // without text.
    private static string ExtractText(byte[] pdfContent, string fileName)
    {
        try
        {
            using var document = PdfDocument.Open(pdfContent);

            var textBuilder = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                _ = textBuilder.AppendLine(page.Text);
            }

            return textBuilder.ToString();
        }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new NoExtractableTextException(
                $"'{fileName}' is password-protected. Remove the password and upload it again.", ex);
        }
        catch (PdfDocumentFormatException ex)
        {
            throw new NoExtractableTextException(
                $"'{fileName}' could not be read as a PDF. The file may be damaged or not a PDF at all.", ex);
        }
    }
}
