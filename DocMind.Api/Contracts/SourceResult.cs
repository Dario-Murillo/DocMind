namespace DocMind.Api.Contracts;

public record SourceResult(string DocumentId, int SequenceNumber, float Score, string Excerpt);
