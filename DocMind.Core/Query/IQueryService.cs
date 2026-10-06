namespace DocMind.Core.Query;

public interface IQueryService
{
    public Task<QueryResult> AskAsync(Guid userId, string question, int topK = 5);
}
