namespace DocMind.Core.Embeddings;

public interface IEmbeddingService
{
    public Task<float[]> GenerateEmbeddingAsync(string text);
}
