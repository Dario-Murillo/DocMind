namespace DocMind.Core.Query;

using DocMind.Core.Completion;
using DocMind.Core.Embeddings;
using DocMind.Core.VectorStore;

public class QueryService(
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    ICompletionService completionService) : IQueryService
{
    private readonly IEmbeddingService embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
    private readonly IVectorStoreService vectorStoreService = vectorStoreService ?? throw new ArgumentNullException(nameof(vectorStoreService));
    private readonly ICompletionService completionService = completionService ?? throw new ArgumentNullException(nameof(completionService));

    public async Task<QueryResult> AskAsync(string question, int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must not be null or empty.", nameof(question));
        }

        var queryVector = await this.embeddingService.GenerateEmbeddingAsync(question);
        var scoredChunks = this.vectorStoreService.Search(queryVector, topK);
        var contextChunks = scoredChunks.Select(scored => scored.Chunk).ToList();

        var answer = await this.completionService.GenerateAnswerAsync(question, contextChunks);

        return new QueryResult(answer, scoredChunks);
    }
}
