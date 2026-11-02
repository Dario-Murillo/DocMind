namespace DocMind.Api.Contracts;

using DocMind.Core.Notebooks;

public record NotebookResponse(Guid NotebookId, string Title, DateTimeOffset CreatedAt, int DocumentCount)
{
    public static NotebookResponse From(NotebookSummary summary) =>
        new(summary.Id, summary.Title, summary.CreatedAt, summary.DocumentCount);
}
