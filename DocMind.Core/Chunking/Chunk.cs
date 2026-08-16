namespace DocMind.Core.Chunking;

public record Chunk(Guid Id, string DocumentId, string Content, int TokenCount, int SequenceNumber);
