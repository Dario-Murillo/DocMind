namespace DocMind.Core.Documents;

public interface IDocumentService
{
    public Task<Guid> IndexDocumentAsync(Guid userId, Stream pdfStream, string fileName);

    public Task<List<DocumentSummary>> ListDocumentsAsync(Guid userId);

    public Task<DocumentFileContent?> GetFileAsync(Guid userId, Guid documentId);

    public Task<bool> DeleteDocumentAsync(Guid userId, Guid documentId);
}
