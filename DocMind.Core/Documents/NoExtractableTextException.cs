namespace DocMind.Core.Documents;

// Distinct from the base InvalidOperationException so callers (e.g. the API layer) can tell
// "no text in this PDF" apart from other InvalidOperationExceptions such as Ollama being unreachable.
public class NoExtractableTextException : InvalidOperationException
{
    public NoExtractableTextException()
    {
    }

    public NoExtractableTextException(string message)
        : base(message)
    {
    }

    public NoExtractableTextException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
