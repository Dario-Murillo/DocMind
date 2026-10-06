namespace DocMind.Core.Documents;

// What the document list needs: a document's metadata, without its file or chunks.
public record DocumentSummary(Guid Id, string FileName, long SizeBytes, DateTimeOffset CreatedAt);