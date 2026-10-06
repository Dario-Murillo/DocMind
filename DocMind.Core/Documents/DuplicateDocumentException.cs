namespace DocMind.Core.Documents;

// Thrown when a user uploads a file whose contents they have already uploaded, so the API can
// answer 409 Conflict instead of indexing (and paying for the embeddings of) the same file twice.
public class DuplicateDocumentException : Exception
{
    public DuplicateDocumentException()
    {
    }

    public DuplicateDocumentException(string message)
        : base(message)
    {
    }

    public DuplicateDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
