namespace DocMind.Core.Query;

public interface IQueryService
{
    public Task<QueryResult> AskAsync(Guid userId, Guid notebookId, string question, int topK = 5);
}
