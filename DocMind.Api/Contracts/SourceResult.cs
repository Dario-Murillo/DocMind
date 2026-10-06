namespace DocMind.Api.Contracts;

public record SourceResult(Guid DocumentId, int SequenceNumber, float Score, string Excerpt);
