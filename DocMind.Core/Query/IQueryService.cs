namespace DocMind.Core.Query;

public interface IQueryService
{
    public Task<QueryResult> AskAsync(string question, int topK = 5);
}
