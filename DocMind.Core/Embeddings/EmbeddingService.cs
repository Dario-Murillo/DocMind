using Microsoft.Extensions.AI;
using OllamaSharp;

namespace DocMind.Core.Embeddings;

public class EmbeddingService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, Uri? endpoint = null, string modelId = EmbeddingService.DefaultModelId) : IEmbeddingService
{
    public const string DefaultModelId = "nomic-embed-text";
    private static readonly Uri DefaultEndpoint = new("http://localhost:11434");

    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
    private readonly Uri _endpoint = endpoint ?? DefaultEndpoint;
    private readonly string _modelId = modelId;

    public EmbeddingService(Uri? endpoint = null, string modelId = DefaultModelId)
        : this(new OllamaApiClient(endpoint ?? DefaultEndpoint, modelId), endpoint ?? DefaultEndpoint, modelId)
    {
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be null or empty.", nameof(text));

        try
        {
            var embeddings = await _embeddingGenerator.GenerateAsync([text]);
            return embeddings[0].Vector.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"Could not reach Ollama to generate the embedding. Ensure Ollama is running ('ollama serve') at {_endpoint} and that the '{_modelId}' model is pulled ('ollama pull {_modelId}').",
                ex);
        }
    }
}
