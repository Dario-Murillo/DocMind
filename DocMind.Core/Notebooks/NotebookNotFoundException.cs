namespace DocMind.Core.Notebooks;

// Thrown when a notebook doesn't exist or belongs to another user, so the API can answer 404 from
// every route nested under a notebook.
public class NotebookNotFoundException : Exception
{
    public NotebookNotFoundException()
    {
    }

    public NotebookNotFoundException(string message)
        : base(message)
    {
    }

    public NotebookNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
