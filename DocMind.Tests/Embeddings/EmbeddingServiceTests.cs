namespace DocMind.Tests.Embeddings;


using DocMind.Core.Embeddings;
using Microsoft.Extensions.AI;

public class EmbeddingServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateEmbeddingAsyncNullOrEmptyTextThrowsArgumentException(string? text)
    {
        // A fake generator that throws if it is ever invoked proves validation
        // happens before any call reaches Ollama.
        var service = new EmbeddingService(new UnreachableEmbeddingGenerator());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateEmbeddingAsync(text!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GenerateEmbeddingAsyncValidTextReturnsNonEmptyFloatArray()
    {
        // Requires a local Ollama instance running with the nomic-embed-text model pulled.
        var service = new EmbeddingService();

        var embedding = await service.GenerateEmbeddingAsync("The quick brown fox jumps over the lazy dog.");

        Assert.NotEmpty(embedding);
    }

    private sealed class UnreachableEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("GenerateAsync must not be called for invalid input.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
