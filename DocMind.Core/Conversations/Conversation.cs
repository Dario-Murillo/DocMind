namespace DocMind.Core.Conversations;

public class Conversation
{
    public const int MaxTitleLength = 100;

    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    // The first question, cut to MaxTitleLength, so the list has a title without asking the
    // model to summarize the conversation.
    public required string Title { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Moved forward on every exchange: the list shows the most recently used conversations first.
    public DateTimeOffset UpdatedAt { get; set; }

    public List<Message> Messages { get; } = [];
}