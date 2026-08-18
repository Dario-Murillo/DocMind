using System.Collections.Concurrent;
using System.Numerics.Tensors;
using DocMind.Core.Chunking;

namespace DocMind.Core.VectorStore;

public class InMemoryVectorStoreService : IVectorStoreService
{
    private readonly ConcurrentQueue<(Chunk Chunk, float[] Vector)> _entries = new();

    public void Add(Chunk chunk, float[] vector)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (vector is null || vector.Length == 0)
            throw new ArgumentException("Vector must not be null or empty.", nameof(vector));

        _entries.Enqueue((chunk, vector));
    }

    public List<ScoredChunk> Search(float[] queryVector, int topK = 5)
    {
        if (queryVector is null || queryVector.Length == 0)
            throw new ArgumentException("Query vector must not be null or empty.", nameof(queryVector));

        return [.. _entries
            .Select(entry => new ScoredChunk(entry.Chunk, TensorPrimitives.CosineSimilarity(queryVector, entry.Vector)))
            .OrderByDescending(scored => scored.Score)
            .Take(topK)];
    }
}
