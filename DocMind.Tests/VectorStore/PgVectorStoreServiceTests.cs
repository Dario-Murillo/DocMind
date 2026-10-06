namespace DocMind.Tests.VectorStore;

using DocMind.Core.Documents;
using DocMind.Core.VectorStore;
using DocMind.Tests.Api;
using Pgvector;

// Runs against the Postgres container shared by the Api collection, so the ordering and scores come
// from pgvector's <=> operator itself. Chunks are inserted directly rather than through
// DocumentService, so each test controls the exact vectors being searched.
[Collection(ApiCollectionDefinition.Name)]
public class PgVectorStoreServiceTests(DocMindApiFactory factory)
{
    [Fact]
    public async Task SearchAsyncUserWithoutDocumentsReturnsEmptyList()
    {
        var userId = await factory.CreateUserAsync();
        await using var dbContext = factory.CreateDbContext();
        var store = new PgVectorStoreService(dbContext);

        var results = await store.SearchAsync(userId, Embedding(1f, 0f));

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsyncIdenticalVectorScoreIsCloseToOne()
    {
        var userId = await factory.CreateUserAsync();
        var vector = Embedding(0.5f, -0.3f);
        await this.SeedChunksAsync(userId, ("identical", vector));
        await using var dbContext = factory.CreateDbContext();
        var store = new PgVectorStoreService(dbContext);

        var results = await store.SearchAsync(userId, vector);

        var result = Assert.Single(results);
        Assert.True(Math.Abs(result.Score - 1.0f) < 0.001f, $"Expected score close to 1.0, got {result.Score}");
    }

    [Fact]
    public async Task SearchAsyncReturnsTopKOrderedByDescendingScore()
    {
        var userId = await factory.CreateUserAsync();
        await this.SeedChunksAsync(
            userId,
            ("far", Embedding(0.1f, 1f)),
            ("close", Embedding(1f, 0.01f)),
            ("opposite", Embedding(-1f, 0f)),
            ("medium", Embedding(1f, 0.5f)));
        await using var dbContext = factory.CreateDbContext();
        var store = new PgVectorStoreService(dbContext);

        var results = await store.SearchAsync(userId, Embedding(1f, 0f), topK: 3);

        Assert.Equal(["close", "medium", "far"], results.Select(result => result.Chunk.Content));
        Assert.True(results[0].Score > results[1].Score);
        Assert.True(results[1].Score > results[2].Score);
    }

    [Fact]
    public async Task SearchAsyncTopKGreaterThanStoredCountReturnsAll()
    {
        var userId = await factory.CreateUserAsync();
        await this.SeedChunksAsync(userId, ("first", Embedding(1f, 0f)), ("second", Embedding(0f, 1f)));
        await using var dbContext = factory.CreateDbContext();
        var store = new PgVectorStoreService(dbContext);

        var results = await store.SearchAsync(userId, Embedding(1f, 1f), topK: 50);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsyncOnlyReturnsChunksOfTheGivenUser()
    {
        var ownerId = await factory.CreateUserAsync();
        var otherUserId = await factory.CreateUserAsync();
        var vector = Embedding(1f, 0f);
        await this.SeedChunksAsync(ownerId, ("owner's chunk", vector));
        await this.SeedChunksAsync(otherUserId, ("other user's chunk", Embedding(0f, 1f)));
        await using var dbContext = factory.CreateDbContext();
        var store = new PgVectorStoreService(dbContext);

        // The other user searches with a vector identical to the owner's chunk: the best match in
        // the whole table must still be invisible to them.
        var results = await store.SearchAsync(otherUserId, vector);

        var result = Assert.Single(results);
        Assert.Equal("other user's chunk", result.Chunk.Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new float[0])]
    public async Task SearchAsyncNullOrEmptyQueryVectorThrowsArgumentException(float[]? queryVector)
    {
        await using var dbContext = factory.CreateDbContext();
        var store = new PgVectorStoreService(dbContext);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => store.SearchAsync(Guid.NewGuid(), queryVector!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsyncNonPositiveTopKThrowsArgumentOutOfRangeException(int topK)
    {
        await using var dbContext = factory.CreateDbContext();
        var store = new PgVectorStoreService(dbContext);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.SearchAsync(Guid.NewGuid(), Embedding(1f, 0f), topK));
    }

    // A vector with the column's 768 dimensions whose only non-zero components are the first two,
    // so tests can reason about angles as if the vectors were 2D.
    private static float[] Embedding(float x, float y)
    {
        var vector = new float[DocumentChunk.EmbeddingDimensions];
        vector[0] = x;
        vector[1] = y;
        return vector;
    }

    // Stores one document owned by userId with one chunk per (content, embedding) pair.
    private async Task SeedChunksAsync(Guid userId, params (string Content, float[] Embedding)[] chunks)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = "seed.pdf",
            SizeBytes = 0,
            Sha256 = $"{Guid.NewGuid():N}{Guid.NewGuid():N}",
            EmbeddingModel = "test",
            CreatedAt = DateTimeOffset.UtcNow,
            File = new DocumentFile { Content = [] },
        };

        for (var i = 0; i < chunks.Length; i++)
        {
            document.Chunks.Add(new DocumentChunk
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SequenceNumber = i,
                Content = chunks[i].Content,
                TokenCount = 1,
                Embedding = new Vector(chunks[i].Embedding),
            });
        }

        await using var dbContext = factory.CreateDbContext();
        _ = dbContext.Documents.Add(document);
        _ = await dbContext.SaveChangesAsync();
    }
}
