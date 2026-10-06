namespace DocMind.Core.Documents;

public interface IDocumentService
{
    public Task<Guid> IndexDocumentAsync(Guid userId, Stream pdfStream, string fileName);
}
