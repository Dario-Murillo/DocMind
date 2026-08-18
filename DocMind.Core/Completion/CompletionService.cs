using System.Text;
using DocMind.Core.Chunking;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace DocMind.Core.Completion;

public class CompletionService(IChatClient chatClient, Uri? endpoint = null, string modelId = CompletionService.DefaultModelId) : ICompletionService
{
    public const string DefaultModelId = "llama3.1";
    private static readonly Uri DefaultEndpoint = new("http://localhost:11434");

    private readonly IChatClient _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    private readonly Uri _endpoint = endpoint ?? DefaultEndpoint;
    private readonly string _modelId = modelId;

    public CompletionService(Uri? endpoint = null, string modelId = DefaultModelId)
        : this(new OllamaApiClient(endpoint ?? DefaultEndpoint, modelId), endpoint ?? DefaultEndpoint, modelId)
    {
    }

    public async Task<string> GenerateAnswerAsync(string question, List<Chunk> contextChunks)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question must not be null or empty.", nameof(question));
        if (contextChunks is null)
            throw new ArgumentException("Context chunks must not be null.", nameof(contextChunks));

        var prompt = BuildPrompt(question, contextChunks);

        try
        {
            var response = await _chatClient.GetResponseAsync(prompt);
            return response.Text;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"Could not reach Ollama to generate the answer. Ensure Ollama is running ('ollama serve') at {_endpoint} and that the '{_modelId}' model is pulled ('ollama pull {_modelId}').",
                ex);
        }
    }

    private static string BuildPrompt(string question, List<Chunk> contextChunks)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Answer the question using ONLY the context below.");
        prompt.AppendLine("If the answer is not contained in the context, say that you don't have enough information to answer instead of making something up.");
        prompt.AppendLine();
        prompt.AppendLine("Context:");
        prompt.AppendLine("---");

        if (contextChunks.Count == 0)
        {
            prompt.AppendLine("(no context available)");
        }
        else
        {
            foreach (var chunk in contextChunks)
            {
                prompt.AppendLine(chunk.Content);
                prompt.AppendLine("---");
            }
        }

        prompt.AppendLine();
        prompt.Append("Question: ").Append(question);

        return prompt.ToString();
    }
}
