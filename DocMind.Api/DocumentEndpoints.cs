namespace DocMind.Api;

using System.Security.Claims;
using DocMind.Api.Contracts;
using DocMind.Core.Documents;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        // A document's id and owner are enough to reach it, so only the routes that need to know
        // the notebook are nested under one.
        var notebookDocuments = app.MapGroup("/notebooks/{notebookId:guid}/documents").WithTags("Documents").RequireAuthorization();
        var documents = app.MapGroup("/documents").WithTags("Documents").RequireAuthorization();

        _ = notebookDocuments.MapPost(string.Empty, async (Guid notebookId, HttpRequest request, ClaimsPrincipal user, IDocumentService documentService) =>
        {
            // Read the form manually rather than binding an IFormFile parameter: the automatic binder
            // short-circuits to a bare, message-less 400 (or throws, depending on ASP.NET Core version
            // and environment) when the request has no body at all, which bypasses our own validation
            // and the exception-handling middleware alike. Reading the form ourselves guarantees a
            // consistent, descriptive 400 in every "no file" scenario.
            var file = request.HasFormContentType ? (await request.ReadFormAsync()).Files["file"] : null;
            if (file is null)
            {
                return Results.BadRequest(new ErrorResponse("A PDF file must be provided."));
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new ErrorResponse($"Only .pdf files are supported. Received '{file.FileName}'."));
            }

            await using var stream = file.OpenReadStream();
            var documentId = await documentService.IndexDocumentAsync(user.GetUserId(), notebookId, stream, file.FileName);

            return Results.Ok(new UploadDocumentResponse(documentId, file.FileName, "Document indexed successfully."));
        })
        .Accepts<IFormFile>("multipart/form-data")
        .WithName("UploadDocument")
        .WithSummary("Uploads a PDF document to a notebook and indexes it")
        .WithDescription("Extracts text from the uploaded PDF (25 MB max), splits it into chunks, generates embeddings for each chunk, and stores the document, the file and the chunks in the notebook. Uploading the same file twice to a notebook returns 409.")
        .Produces<UploadDocumentResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
        .Produces<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)
        .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        _ = notebookDocuments.MapGet(string.Empty, async (Guid notebookId, ClaimsPrincipal user, IDocumentService documentService) =>
        {
            var summaries = await documentService.ListDocumentsAsync(user.GetUserId(), notebookId);

            return Results.Ok(summaries
                .Select(summary => new DocumentResponse(summary.Id, summary.FileName, summary.SizeBytes, summary.CreatedAt))
                .ToList());
        })
        .WithName("ListDocuments")
        .WithSummary("Lists the documents of one of the signed-in user's notebooks, newest first")
        .Produces<List<DocumentResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        _ = documents.MapGet("/{documentId:guid}/file", async (Guid documentId, ClaimsPrincipal user, IDocumentService documentService) =>
        {
            var file = await documentService.GetFileAsync(user.GetUserId(), documentId);

            return file is null
                ? Results.NotFound(new ErrorResponse("Document not found."))
                : Results.File(file.Content, "application/pdf", file.FileName);
        })
        .WithName("DownloadDocumentFile")
        .WithSummary("Downloads the original PDF of one of the signed-in user's documents")
        .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        _ = documents.MapDelete("/{documentId:guid}", async (Guid documentId, ClaimsPrincipal user, IDocumentService documentService) =>
            await documentService.DeleteDocumentAsync(user.GetUserId(), documentId)
                ? Results.NoContent()
                : Results.NotFound(new ErrorResponse("Document not found.")))
        .WithName("DeleteDocument")
        .WithSummary("Deletes one of the signed-in user's documents, with its file and chunks")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }
}
