namespace DocMind.Core.Documents;

public interface IDocumentService
{
    Task<string> IndexDocumentAsync(Stream pdfStream, string fileName);

    Task<string> IndexPlainTextAsync(string text, string documentName);
}
