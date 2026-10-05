namespace DocMind.Core.Documents;

public class Document
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string FileName { get; set; }

    public long SizeBytes { get; set; }

    // Hex-encoded SHA-256 of the PDF bytes, used to detect a user uploading the same file twice.
    public required string Sha256 { get; set; }

    // The model that produced the chunk embeddings, so documents can be found and re-indexed if
    // the embedding model ever changes (vectors from different models are not comparable).
    public required string EmbeddingModel { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DocumentFile? File { get; set; }

    public List<DocumentChunk> Chunks { get; } = [];
}