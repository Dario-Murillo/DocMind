namespace DocMind.Core.Conversations;

// What the conversation list needs: a conversation's metadata, without its messages.
public record ConversationSummary(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
