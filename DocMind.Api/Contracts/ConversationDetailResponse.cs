namespace DocMind.Api.Contracts;

using DocMind.Core.Conversations;

public record ConversationDetailResponse(
    Guid ConversationId,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<MessageResponse> Messages)
{
    public static ConversationDetailResponse From(Conversation conversation) => new(
        conversation.Id,
        conversation.Title,
        conversation.CreatedAt,
        conversation.UpdatedAt,
        [.. conversation.Messages.Select(MessageResponse.From)]);
}
