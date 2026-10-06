namespace DocMind.Api.Contracts;

public record DocumentResponse(Guid DocumentId, string FileName, long SizeBytes, DateTimeOffset CreatedAt);
