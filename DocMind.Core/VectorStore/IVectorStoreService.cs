namespace DocMind.Core.VectorStore;
using DocMind.Core.Chunking;

public interface IVectorStoreService
{
    public void Add(Chunk chunk, float[] vector);

    public List<ScoredChunk> Search(float[] queryVector, int topK = 5);
}
