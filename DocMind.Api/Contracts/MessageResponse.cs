namespace DocMind.Api.Contracts;

using DocMind.Core.Conversations;

// Role is "user" or "assistant", the values the UI's ChatMessage already uses, rather than the
// enum's number, which is a storage detail.
public record MessageResponse(Guid MessageId, string Role, string Content, DateTimeOffset CreatedAt, List<SourceResult> Sources)
{
    public static MessageResponse From(Message message) => new(
        message.Id,
        message.Role == MessageRole.User ? "user" : "assistant",
        message.Content,
        message.CreatedAt,
        [.. message.Sources.Select(SourceResult.From)]);
}
