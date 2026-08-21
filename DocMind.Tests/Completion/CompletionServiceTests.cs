namespace DocMind.Tests.Completion;


using DocMind.Core.Chunking;
using DocMind.Core.Completion;
using Microsoft.Extensions.AI;

public class CompletionServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAnswerAsyncNullOrEmptyQuestionThrowsArgumentException(string? question)
    {
        var service = new CompletionService(new UnreachableChatClient());

        _ = await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAnswerAsync(question!, []));
    }

    [Fact]
    public async Task GenerateAnswerAsyncNullContextChunksThrowsArgumentException()
    {
        var service = new CompletionService(new UnreachableChatClient());

        _ = await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAnswerAsync("What is DocMind?", null!));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GenerateAnswerAsyncValidQuestionWithContextReturnsNonEmptyAnswer()
    {
        // Requires a local Ollama instance running with the llama3.1 model pulled.
        var service = new CompletionService();
        var context = new List<Chunk>
        {
            new(Guid.NewGuid(), "doc1", "DocMind is a local, self-hosted RAG document Q&A system built on .NET 9.", TokenCount: 15, SequenceNumber: 0),
        };

        var answer = await service.GenerateAnswerAsync("What is DocMind?", context);

        Assert.False(string.IsNullOrWhiteSpace(answer));
    }

    private sealed class UnreachableChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("GetResponseAsync must not be called for invalid input.");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("GetStreamingResponseAsync must not be called for invalid input.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
