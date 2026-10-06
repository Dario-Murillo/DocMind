namespace DocMind.Core.Documents;

using Pgvector;

public class DocumentChunk
{
    // nomic-embed-text produces 768-dimensional vectors. pgvector checks every inserted vector
    // against the column's declared size.
    public const int EmbeddingDimensions = 768;

    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    // Duplicated from Document on purpose: vector search filters by owner without joining Documents.
    public Guid UserId { get; set; }

    public int SequenceNumber { get; set; }

    public required string Content { get; set; }

    public int TokenCount { get; set; }

    public required Vector Embedding { get; set; }
}