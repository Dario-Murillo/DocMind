namespace DocMind.Api.Contracts;

using DocMind.Core.Conversations;

public record ConversationResponse(Guid ConversationId, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static ConversationResponse From(ConversationSummary summary) =>
        new(summary.Id, summary.Title, summary.CreatedAt, summary.UpdatedAt);
}
