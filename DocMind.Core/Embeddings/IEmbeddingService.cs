namespace DocMind.Core.Embeddings;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text);
}
