namespace DocMind.Core.Documents;

public interface IDocumentService
{
    public Task<Guid> IndexDocumentAsync(Stream pdfStream, string fileName);

    public Task<Guid> IndexPlainTextAsync(string text, string documentName);
}
