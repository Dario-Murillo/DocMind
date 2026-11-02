namespace DocMind.Api;

using System.Security.Claims;
using DocMind.Api.Contracts;
using DocMind.Core.Notebooks;

public static class NotebookEndpoints
{
    public static IEndpointRouteBuilder MapNotebookEndpoints(this IEndpointRouteBuilder app)
    {
        var notebooks = app.MapGroup("/notebooks").WithTags("Notebooks").RequireAuthorization();

        _ = notebooks.MapPost(string.Empty, async (NotebookRequest request, ClaimsPrincipal user, INotebookService notebookService) =>
        {
            var notebook = await notebookService.CreateNotebookAsync(user.GetUserId(), request.Title);

            return Results.Created($"/notebooks/{notebook.Id}", NotebookResponse.From(notebook));
        })
        .WithName("CreateNotebook")
        .WithSummary("Creates a notebook for the signed-in user")
        .Produces<NotebookResponse>(StatusCodes.Status201Created)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        _ = notebooks.MapGet(string.Empty, async (ClaimsPrincipal user, INotebookService notebookService) =>
        {
            var summaries = await notebookService.ListNotebooksAsync(user.GetUserId());

            return Results.Ok(summaries.Select(NotebookResponse.From).ToList());
        })
        .WithName("ListNotebooks")
        .WithSummary("Lists the signed-in user's notebooks, newest first")
        .Produces<List<NotebookResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        _ = notebooks.MapGet("/{notebookId:guid}", async (Guid notebookId, ClaimsPrincipal user, INotebookService notebookService) =>
        {
            var notebook = await notebookService.GetNotebookAsync(user.GetUserId(), notebookId);

            return notebook is null
                ? Results.NotFound(new ErrorResponse("Notebook not found."))
                : Results.Ok(NotebookResponse.From(notebook));
        })
        .WithName("GetNotebook")
        .WithSummary("Gets one of the signed-in user's notebooks")
        .Produces<NotebookResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        _ = notebooks.MapPatch("/{notebookId:guid}", async (Guid notebookId, NotebookRequest request, ClaimsPrincipal user, INotebookService notebookService) =>
        {
            var notebook = await notebookService.RenameNotebookAsync(user.GetUserId(), notebookId, request.Title);

            return notebook is null
                ? Results.NotFound(new ErrorResponse("Notebook not found."))
                : Results.Ok(NotebookResponse.From(notebook));
        })
        .WithName("RenameNotebook")
        .WithSummary("Renames one of the signed-in user's notebooks")
        .Produces<NotebookResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        _ = notebooks.MapDelete("/{notebookId:guid}", async (Guid notebookId, ClaimsPrincipal user, INotebookService notebookService) =>
            await notebookService.DeleteNotebookAsync(user.GetUserId(), notebookId)
                ? Results.NoContent()
                : Results.NotFound(new ErrorResponse("Notebook not found.")))
        .WithName("DeleteNotebook")
        .WithSummary("Deletes one of the signed-in user's notebooks with all of its documents and conversations")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }
}
