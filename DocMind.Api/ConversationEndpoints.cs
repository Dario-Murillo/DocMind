namespace DocMind.Api;

using System.Security.Claims;
using DocMind.Api.Contracts;
using DocMind.Core.Conversations;

public static class ConversationEndpoints
{
    private const int DefaultTopK = 5;

    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var conversations = app.MapGroup("/conversations").WithTags("Conversations").RequireAuthorization();

        _ = conversations.MapPost(string.Empty, async (AskRequest request, ClaimsPrincipal user, IConversationService conversationService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Results.BadRequest(new ErrorResponse("Question must not be null or empty."));
            }

            var exchange = await conversationService.StartConversationAsync(user.GetUserId(), request.Question, request.TopK ?? DefaultTopK);

            return Results.Created($"/conversations/{exchange.Conversation.Id}", ExchangeResponse.From(exchange));
        })
        .WithName("StartConversation")
        .WithSummary("Starts a conversation with its first question")
        .WithDescription("Answers the question with retrieval-augmented generation over the signed-in user's documents, then stores a new conversation, titled after the question, holding the question and the answer. If the answer can't be generated, nothing is stored.")
        .Produces<ExchangeResponse>(StatusCodes.Status201Created)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        _ = conversations.MapPost("/{conversationId:guid}/messages", async (Guid conversationId, AskRequest request, ClaimsPrincipal user, IConversationService conversationService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return Results.BadRequest(new ErrorResponse("Question must not be null or empty."));
            }

            var exchange = await conversationService.AddMessageAsync(user.GetUserId(), conversationId, request.Question, request.TopK ?? DefaultTopK);

            return exchange is null
                ? Results.NotFound(new ErrorResponse("Conversation not found."))
                : Results.Ok(ExchangeResponse.From(exchange));
        })
        .WithName("AddMessage")
        .WithSummary("Asks a new question in one of the signed-in user's conversations")
        .WithDescription("Answers the question the same way as starting a conversation and appends the question and the answer to it. Each question is answered on its own; earlier messages aren't sent to the model.")
        .Produces<ExchangeResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        _ = conversations.MapGet(string.Empty, async (ClaimsPrincipal user, IConversationService conversationService) =>
        {
            var summaries = await conversationService.ListConversationsAsync(user.GetUserId());

            return Results.Ok(summaries.Select(ConversationResponse.From).ToList());
        })
        .WithName("ListConversations")
        .WithSummary("Lists the signed-in user's conversations, most recently used first")
        .Produces<List<ConversationResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        _ = conversations.MapGet("/{conversationId:guid}", async (Guid conversationId, ClaimsPrincipal user, IConversationService conversationService) =>
        {
            var conversation = await conversationService.GetConversationAsync(user.GetUserId(), conversationId);

            return conversation is null
                ? Results.NotFound(new ErrorResponse("Conversation not found."))
                : Results.Ok(ConversationDetailResponse.From(conversation));
        })
        .WithName("GetConversation")
        .WithSummary("Gets one of the signed-in user's conversations with its messages and their sources")
        .Produces<ConversationDetailResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        _ = conversations.MapDelete("/{conversationId:guid}", async (Guid conversationId, ClaimsPrincipal user, IConversationService conversationService) =>
            await conversationService.DeleteConversationAsync(user.GetUserId(), conversationId)
                ? Results.NoContent()
                : Results.NotFound(new ErrorResponse("Conversation not found.")))
        .WithName("DeleteConversation")
        .WithSummary("Deletes one of the signed-in user's conversations with its messages")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }
}
