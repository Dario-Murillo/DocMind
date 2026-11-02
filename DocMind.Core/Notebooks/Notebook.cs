namespace DocMind.Core.Notebooks;

// Groups a user's documents and conversations: a question is only answered from the documents of
// the notebook it was asked in.
public class Notebook
{
    public const int MaxTitleLength = 100;

    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
