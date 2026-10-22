namespace DocMind.Core.Conversations;

public class Message
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public MessageRole Role { get; set; }

    public required string Content { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Only assistant messages have sources: the chunks their answer was based on.
    public List<MessageSource> Sources { get; } = [];
}
