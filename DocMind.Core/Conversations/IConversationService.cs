namespace DocMind.Core.Conversations;

public interface IConversationService
{
    public Task<ConversationExchange> StartConversationAsync(Guid userId, string question, int topK = 5);

    public Task<ConversationExchange?> AddMessageAsync(Guid userId, Guid conversationId, string question, int topK = 5);

    public Task<List<ConversationSummary>> ListConversationsAsync(Guid userId);

    public Task<Conversation?> GetConversationAsync(Guid userId, Guid conversationId);

    public Task<bool> DeleteConversationAsync(Guid userId, Guid conversationId);
}
