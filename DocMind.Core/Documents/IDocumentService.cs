namespace DocMind.Core.Documents;

public interface IDocumentService
{
    public Task<string> IndexDocumentAsync(Stream pdfStream, string fileName);

    public Task<string> IndexPlainTextAsync(string text, string documentName);
}
