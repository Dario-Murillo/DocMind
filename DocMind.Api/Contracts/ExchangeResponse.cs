namespace DocMind.Api.Contracts;

using DocMind.Core.Conversations;

// Returned by both "ask" endpoints. Conversation lets the UI add a new conversation to its list,
// or move an existing one to the top, without fetching the list again.
public record ExchangeResponse(ConversationResponse Conversation, MessageResponse Question, MessageResponse Answer)
{
    public static ExchangeResponse From(ConversationExchange exchange) => new(
        ConversationResponse.From(exchange.Conversation),
        MessageResponse.From(exchange.Question),
        MessageResponse.From(exchange.Answer));
}