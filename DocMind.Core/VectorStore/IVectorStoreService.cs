using DocMind.Core.Chunking;

namespace DocMind.Core.VectorStore;

public interface IVectorStoreService
{
    void Add(Chunk chunk, float[] vector);

    List<ScoredChunk> Search(float[] queryVector, int topK = 5);
}
