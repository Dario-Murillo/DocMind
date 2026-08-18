namespace DocMind.Tests.VectorStore;


using DocMind.Core.Chunking;
using DocMind.Core.VectorStore;

public class InMemoryVectorStoreServiceTests
{
    [Fact]
    public void SearchEmptyStoreReturnsEmptyList()
    {
        var store = new InMemoryVectorStoreService();

        var results = store.Search([1f, 0f, 0f]);

        Assert.Empty(results);
    }

    [Fact]
    public void SearchQueryIdenticalToStoredVectorScoreIsCloseToOne()
    {
        var store = new InMemoryVectorStoreService();
        var chunk = CreateChunk("doc1", 0);
        float[] vector = [0.5f, 0.2f, -0.3f, 0.8f];
        store.Add(chunk, vector);

        var results = store.Search(vector);

        var result = Assert.Single(results);
        Assert.True(Math.Abs(result.Score - 1.0f) < 0.001f, $"Expected score close to 1.0, got {result.Score}");
    }

    [Fact]
    public void SearchMultipleChunksReturnsTopKOrderedByDescendingScore()
    {
        var store = new InMemoryVectorStoreService();
        var queryVector = new float[] { 1f, 0f };

        var closeMatch = CreateChunk("doc-close", 0);
        var mediumMatch = CreateChunk("doc-medium", 0);
        var farMatch = CreateChunk("doc-far", 0);
        var oppositeMatch = CreateChunk("doc-opposite", 0);

        store.Add(farMatch, [0.1f, 1f]);
        store.Add(closeMatch, [1f, 0.01f]);
        store.Add(oppositeMatch, [-1f, 0f]);
        store.Add(mediumMatch, [1f, 0.5f]);

        var results = store.Search(queryVector, topK: 3);

        Assert.Equal(3, results.Count);
        Assert.Equal(closeMatch, results[0].Chunk);
        Assert.Equal(mediumMatch, results[1].Chunk);
        Assert.Equal(farMatch, results[2].Chunk);
        Assert.True(results[0].Score > results[1].Score);
        Assert.True(results[1].Score > results[2].Score);
    }

    [Fact]
    public void SearchTopKGreaterThanStoredCountReturnsAllWithoutError()
    {
        var store = new InMemoryVectorStoreService();
        store.Add(CreateChunk("doc1", 0), [1f, 0f]);
        store.Add(CreateChunk("doc2", 1), [0f, 1f]);

        var results = store.Search([1f, 1f], topK: 50);

        Assert.Equal(2, results.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new float[0])]
    public void SearchNullOrEmptyQueryVectorThrowsArgumentException(float[]? queryVector)
    {
        var store = new InMemoryVectorStoreService();

        _ = Assert.Throws<ArgumentException>(() => store.Search(queryVector!));
    }

    private static Chunk CreateChunk(string documentId, int sequenceNumber) =>
        new(Guid.NewGuid(), documentId, $"content for {documentId}", TokenCount: 10, sequenceNumber);
}
