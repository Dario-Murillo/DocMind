namespace DocMind.Core.Completion;


using System.Text;
using DocMind.Core.Chunking;
using Microsoft.Extensions.AI;
using OllamaSharp;

public class CompletionService(IChatClient chatClient, Uri? endpoint = null, string modelId = CompletionService.DefaultModelId) : ICompletionService
{
    public const string DefaultModelId = "llama3.1";
    private static readonly Uri DefaultEndpoint = new("http://localhost:11434");

    // Loading llama3.1 and reading a few thousand tokens of context can take minutes when most of
    // the model runs on the CPU. HttpClient's default timeout (100 s) cut those answers off.
    // nginx's proxy_read_timeout (300 s) stays above it, so a timeout still reaches the UI as
    // this service's 503 instead of a bare 504 from nginx.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(4);

    private readonly IChatClient chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    private readonly Uri endpoint = endpoint ?? DefaultEndpoint;
    private readonly string modelId = modelId;

    public CompletionService(Uri? endpoint = null, string modelId = DefaultModelId)
        : this(CreateClient(endpoint ?? DefaultEndpoint, modelId), endpoint ?? DefaultEndpoint, modelId)
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
        // HttpClient reports its own timeout as a TaskCanceledException wrapping a TimeoutException.
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new InvalidOperationException(
                $"Ollama took longer than {RequestTimeout.TotalMinutes} minutes to generate the answer with '{this.modelId}'. The model may be running mostly on the CPU; try again once it has loaded.",
                ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"Could not reach Ollama to generate the answer. Ensure Ollama is running ('ollama serve') at {this.endpoint} and that the '{this.modelId}' model is pulled ('ollama pull {this.modelId}').",
                ex);
        }
    }

    private static OllamaApiClient CreateClient(Uri endpoint, string modelId) =>
        new(new HttpClient { BaseAddress = endpoint, Timeout = RequestTimeout }, modelId);

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
