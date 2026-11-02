namespace DocMind.Core.Notebooks;

public interface INotebookService
{
    public Task<NotebookSummary> CreateNotebookAsync(Guid userId, string title);

    public Task<List<NotebookSummary>> ListNotebooksAsync(Guid userId);

    public Task<NotebookSummary?> GetNotebookAsync(Guid userId, Guid notebookId);

    public Task<NotebookSummary?> RenameNotebookAsync(Guid userId, Guid notebookId, string title);

    public Task<bool> DeleteNotebookAsync(Guid userId, Guid notebookId);
}
