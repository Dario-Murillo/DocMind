namespace DocMind.Core.Notebooks;

public record NotebookSummary(Guid Id, string Title, DateTimeOffset CreatedAt, int DocumentCount);
