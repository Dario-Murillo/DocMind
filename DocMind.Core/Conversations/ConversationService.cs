namespace DocMind.Core.Conversations;

using DocMind.Core.Data;
using DocMind.Core.Query;
using DocMind.Core.VectorStore;
using Microsoft.EntityFrameworkCore;

public class ConversationService(IQueryService queryService, DocMindDbContext dbContext) : IConversationService
{
    private const int MaxExcerptLength = 150;

    private readonly IQueryService queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
    private readonly DocMindDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<ConversationExchange> StartConversationAsync(Guid userId, string question, int topK = 5)
    {
        ValidateQuestion(question);

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            UserId = userId,
            Title = BuildTitle(question),
            CreatedAt = now,
            UpdatedAt = now,
        };
        _ = this.dbContext.Conversations.Add(conversation);

        return await this.AskAsync(conversation, question, topK);
    }

    public async Task<ConversationExchange?> AddMessageAsync(Guid userId, Guid conversationId, string question, int topK = 5)
    {
        ValidateQuestion(question);

        var conversation = await this.dbContext.Conversations
            .SingleOrDefaultAsync(conversation => conversation.Id == conversationId && conversation.UserId == userId);

        return conversation is null ? null : await this.AskAsync(conversation, question, topK);
    }

    public Task<List<ConversationSummary>> ListConversationsAsync(Guid userId) =>
        this.dbContext.Conversations
            .Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .Select(conversation => new ConversationSummary(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt))
            .ToListAsync();

    // Messages and their sources are two collections. AsSplitQuery loads each with its own query,
    // instead of a single join that repeats every message once per source.
    public Task<Conversation?> GetConversationAsync(Guid userId, Guid conversationId) =>
        this.dbContext.Conversations
            .AsNoTracking()
            .Include(conversation => conversation.Messages.OrderBy(message => message.CreatedAt).ThenBy(message => message.Role))
            .ThenInclude(message => message.Sources.OrderByDescending(source => source.Score))
            .AsSplitQuery()
            .SingleOrDefaultAsync(conversation => conversation.Id == conversationId && conversation.UserId == userId);

    // A single DELETE; the messages and their sources go with it through ON DELETE CASCADE.
    public async Task<bool> DeleteConversationAsync(Guid userId, Guid conversationId) =>
        await this.dbContext.Conversations
            .Where(conversation => conversation.Id == conversationId && conversation.UserId == userId)
            .ExecuteDeleteAsync() > 0;

    // Runs the RAG pipeline first and stores the question and the answer together, in a single
    // SaveChangesAsync. If Ollama fails, the exception propagates and nothing is stored, not even
    // a new conversation, the same way a failed upload stores nothing.
    private async Task<ConversationExchange> AskAsync(Conversation conversation, string question, int topK)
    {
        // Taken before calling the model, so the answer's timestamp is later than the question's.
        var askedAt = DateTimeOffset.UtcNow;
        var result = await this.queryService.AskAsync(conversation.UserId, question, topK);

        var questionMessage = new Message { Role = MessageRole.User, Content = question, CreatedAt = askedAt };
        var answerMessage = new Message { Role = MessageRole.Assistant, Content = result.Answer, CreatedAt = DateTimeOffset.UtcNow };
        answerMessage.Sources.AddRange(await this.BuildSourcesAsync(conversation.UserId, result.Sources));

        // Ids are left unset on purpose: EF generates them when it finds these entities through the
        // conversation. An entity whose generated key is already set is taken for an existing row,
        // and SaveChangesAsync would try to UPDATE it instead of inserting it.
        conversation.Messages.Add(questionMessage);
        conversation.Messages.Add(answerMessage);
        conversation.UpdatedAt = answerMessage.CreatedAt;

        _ = await this.dbContext.SaveChangesAsync();

        return new ConversationExchange(
            new ConversationSummary(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt),
            questionMessage,
            answerMessage);
    }

    private async Task<List<MessageSource>> BuildSourcesAsync(Guid userId, List<ScoredChunk> scoredChunks)
    {
        var documentIds = scoredChunks.Select(scored => scored.Chunk.DocumentId).Distinct().ToList();
        var fileNames = await this.dbContext.Documents
            .Where(document => document.UserId == userId && documentIds.Contains(document.Id))
            .Select(document => new { document.Id, document.FileName })
            .ToDictionaryAsync(document => document.Id, document => document.FileName);

        return [.. scoredChunks
            .Select(scored => new MessageSource
            {
                DocumentId = scored.Chunk.DocumentId,
                FileName = fileNames[scored.Chunk.DocumentId],
                SequenceNumber = scored.Chunk.SequenceNumber,
                Score = scored.Score,
                Excerpt = BuildExcerpt(scored.Chunk.Content),
            })];
    }

    private static void ValidateQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question must not be null or empty.", nameof(question));
        }
    }

    private static string BuildTitle(string question)
    {
        var title = question.Trim();
        return title.Length <= Conversation.MaxTitleLength
            ? title
            : string.Concat(title.AsSpan(0, Conversation.MaxTitleLength - 1), "…");
    }

    private static string BuildExcerpt(string content) =>
        content.Length <= MaxExcerptLength ? content : string.Concat(content.AsSpan(0, MaxExcerptLength), "...");
}
