namespace DocMind.Api.Contracts;

public record UploadDocumentResponse(Guid DocumentId, string FileName, string Message);
