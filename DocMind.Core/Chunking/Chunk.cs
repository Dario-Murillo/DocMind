namespace DocMind.Core.Chunking;

public record Chunk(Guid Id, Guid DocumentId, string Content, int TokenCount, int SequenceNumber);
