namespace DocMind.Core.Embeddings;

public interface IEmbeddingService
{
    public string ModelId { get; }

    public Task<float[]> GenerateEmbeddingAsync(string text);
}
