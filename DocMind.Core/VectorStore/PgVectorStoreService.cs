namespace DocMind.Core.VectorStore;

using DocMind.Core.Chunking;
using DocMind.Core.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

public class PgVectorStoreService(DocMindDbContext dbContext) : IVectorStoreService
{
    private readonly DocMindDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<List<ScoredChunk>> SearchAsync(Guid userId, float[] queryVector, int topK = 5)
    {
        if (queryVector is null || queryVector.Length == 0)
        {
            throw new ArgumentException("Query vector must not be null or empty.", nameof(queryVector));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);

        var query = new Vector(queryVector);

        // Exact nearest-neighbour search: Postgres computes the cosine distance (<=>) to every chunk
        // of this user, sorts by it and returns the first topK. Score is cosine similarity
        // (1 - distance), the same value the in-memory store used to report.
        return await this.dbContext.DocumentChunks
            .Where(chunk => chunk.UserId == userId)
            .OrderBy(chunk => chunk.Embedding.CosineDistance(query))
            .Take(topK)
            .Select(chunk => new ScoredChunk(
                new Chunk(chunk.Id, chunk.DocumentId, chunk.Content, chunk.TokenCount, chunk.SequenceNumber),
                (float)(1 - chunk.Embedding.CosineDistance(query))))
            .ToListAsync();
    }
}