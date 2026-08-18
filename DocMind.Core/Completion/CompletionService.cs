namespace DocMind.Core.Completion;


using System.Text;
using DocMind.Core.Chunking;
using Microsoft.Extensions.AI;
using OllamaSharp;

public class CompletionService(IChatClient chatClient, Uri? endpoint = null, string modelId = CompletionService.DefaultModelId) : ICompletionService
{
    public const string DefaultModelId = "llama3.1";
    private static readonly Uri DefaultEndpoint = new("http://localhost:11434");

    private readonly IChatClient chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    private readonly Uri endpoint = endpoint ?? DefaultEndpoint;
    private readonly string modelId = modelId;

    public CompletionService(Uri? endpoint = null, string modelId = DefaultModelId)
        : this(new OllamaApiClient(endpoint ?? DefaultEndpoint, modelId), endpoint ?? DefaultEndpoint, modelId)
    {
    }

    public async Task<string> GenerateAnswerAsync(string question, List<Chunk> contextChunks)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must not be null or empty.", nameof(question));
        }

        if (contextChunks is null)
        {
            throw new ArgumentException("Context chunks must not be null.", nameof(contextChunks));
        }

        var prompt = BuildPrompt(question, contextChunks);

        try
        {
            var response = await this.chatClient.GetResponseAsync(prompt);
            return response.Text;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"Could not reach Ollama to generate the answer. Ensure Ollama is running ('ollama serve') at {this.endpoint} and that the '{this.modelId}' model is pulled ('ollama pull {this.modelId}').",
                ex);
        }
    }

    private static string BuildPrompt(string question, List<Chunk> contextChunks)
    {
        var prompt = new StringBuilder()
            .AppendLine("Answer the question using ONLY the context below.")
            .AppendLine("If the answer is not contained in the context, say that you don't have enough information to answer instead of making something up.")
            .AppendLine()
            .AppendLine("Context:")
            .AppendLine("---");

        if (contextChunks.Count == 0)
        {
            prompt = prompt.AppendLine("(no context available)");
        }
        else
        {
            foreach (var chunk in contextChunks)
            {
                prompt = prompt.AppendLine(chunk.Content).AppendLine("---");
            }
        }

        prompt = prompt.AppendLine().Append("Question: ").Append(question);

        return prompt.ToString();
    }
}
