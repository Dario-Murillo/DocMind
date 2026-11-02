namespace DocMind.Core.Notebooks;

using DocMind.Core.Data;
using Microsoft.EntityFrameworkCore;

internal static class NotebookDbContextExtensions
{
    // Services call this before working inside a notebook, and before any call to Ollama, so an
    // empty notebook can be told apart from another user's and no embeddings are wasted on it.
    public static async Task EnsureNotebookExistsAsync(this DocMindDbContext dbContext, Guid userId, Guid notebookId)
    {
        var exists = await dbContext.Notebooks.AnyAsync(notebook => notebook.Id == notebookId && notebook.UserId == userId);
        if (!exists)
        {
            throw new NotebookNotFoundException("Notebook not found.");
        }
    }
}
