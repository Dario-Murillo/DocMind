namespace DocMind.Core.VectorStore;

public interface IVectorStoreService
{
    public Task<List<ScoredChunk>> SearchAsync(Guid userId, Guid notebookId, float[] queryVector, int topK = 5);
}
