namespace DocMind.Core.Documents;

public interface IDocumentService
{
    public Task<Guid> IndexDocumentAsync(Guid userId, Guid notebookId, Stream pdfStream, string fileName);

    public Task<List<DocumentSummary>> ListDocumentsAsync(Guid userId, Guid notebookId);

    public Task<DocumentFileContent?> GetFileAsync(Guid userId, Guid documentId);

    public Task<bool> DeleteDocumentAsync(Guid userId, Guid documentId);
}
