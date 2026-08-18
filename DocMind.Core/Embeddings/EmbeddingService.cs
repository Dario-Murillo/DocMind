namespace DocMind.Core.Embeddings;


using Microsoft.Extensions.AI;
using OllamaSharp;

public class EmbeddingService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, Uri? endpoint = null, string modelId = EmbeddingService.DefaultModelId) : IEmbeddingService
{
    public const string DefaultModelId = "nomic-embed-text";
    private static readonly Uri DefaultEndpoint = new("http://localhost:11434");

    private readonly IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
    private readonly Uri endpoint = endpoint ?? DefaultEndpoint;
    private readonly string modelId = modelId;

    public EmbeddingService(Uri? endpoint = null, string modelId = DefaultModelId)
        : this(new OllamaApiClient(endpoint ?? DefaultEndpoint, modelId), endpoint ?? DefaultEndpoint, modelId)
    {
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text must not be null or empty.", nameof(text));
        }

        try
        {
            var embeddings = await this.embeddingGenerator.GenerateAsync([text]);
            return embeddings[0].Vector.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"Could not reach Ollama to generate the embedding. Ensure Ollama is running ('ollama serve') at {this.endpoint} and that the '{this.modelId}' model is pulled ('ollama pull {this.modelId}').",
                ex);
        }
    }
}
