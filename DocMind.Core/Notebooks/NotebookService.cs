namespace DocMind.Core.Notebooks;

using DocMind.Core.Data;
using Microsoft.EntityFrameworkCore;

public class NotebookService(DocMindDbContext dbContext) : INotebookService
{
    private readonly DocMindDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<NotebookSummary> CreateNotebookAsync(Guid userId, string title)
    {
        var notebook = new Notebook
        {
            UserId = userId,
            Title = NormalizeTitle(title),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _ = this.dbContext.Notebooks.Add(notebook);
        _ = await this.dbContext.SaveChangesAsync();

        return new NotebookSummary(notebook.Id, notebook.Title, notebook.CreatedAt, 0);
    }

    public Task<List<NotebookSummary>> ListNotebooksAsync(Guid userId) =>
        this.dbContext.Notebooks
            .Where(notebook => notebook.UserId == userId)
            .OrderByDescending(notebook => notebook.CreatedAt)
            .Select(notebook => new NotebookSummary(
                notebook.Id,
                notebook.Title,
                notebook.CreatedAt,
                this.dbContext.Documents.Count(document => document.NotebookId == notebook.Id)))
            .ToListAsync();

    public Task<NotebookSummary?> GetNotebookAsync(Guid userId, Guid notebookId) =>
        this.dbContext.Notebooks
            .Where(notebook => notebook.Id == notebookId && notebook.UserId == userId)
            .Select(notebook => new NotebookSummary(
                notebook.Id,
                notebook.Title,
                notebook.CreatedAt,
                this.dbContext.Documents.Count(document => document.NotebookId == notebook.Id)))
            .SingleOrDefaultAsync();

    public async Task<NotebookSummary?> RenameNotebookAsync(Guid userId, Guid notebookId, string title)
    {
        var normalizedTitle = NormalizeTitle(title);

        var notebook = await this.dbContext.Notebooks
            .SingleOrDefaultAsync(notebook => notebook.Id == notebookId && notebook.UserId == userId);
        if (notebook is null)
        {
            return null;
        }

        notebook.Title = normalizedTitle;
        _ = await this.dbContext.SaveChangesAsync();

        return await this.GetNotebookAsync(userId, notebookId);
    }

    // A single DELETE. Its documents and conversations, with their files, chunks, messages and
    // sources, are removed by the database's ON DELETE CASCADE.
    public async Task<bool> DeleteNotebookAsync(Guid userId, Guid notebookId) =>
        await this.dbContext.Notebooks
            .Where(notebook => notebook.Id == notebookId && notebook.UserId == userId)
            .ExecuteDeleteAsync() > 0;

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Notebook title must not be null or empty.", nameof(title));
        }

        var trimmed = title.Trim();
        if (trimmed.Length > Notebook.MaxTitleLength)
        {
            throw new ArgumentException($"Notebook title must not exceed {Notebook.MaxTitleLength} characters.", nameof(title));
        }

        return trimmed;
    }
}
