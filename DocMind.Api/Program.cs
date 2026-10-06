using System.Security.Claims;
using DocMind.Api;
using DocMind.Api.Contracts;
using DocMind.Core.Chunking;
using DocMind.Core.Completion;
using DocMind.Core.Data;
using DocMind.Core.Documents;
using DocMind.Core.Embeddings;
using DocMind.Core.Query;
using DocMind.Core.Users;
using DocMind.Core.VectorStore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

const string angularDevCorsPolicy = "AngularDev";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Lets the Angular dev server (ng serve, default port 4200) call this API directly during
// local development, sending the auth cookie along. Only registered in Development further
// down — see IsDevelopment() below.
builder.Services.AddCors(options => options.AddPolicy(angularDevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

// The chunking tokenizer and the Ollama-backed embedding/completion clients hold no per-request
// state, so they are singletons. The services that use DocMindDbContext are scoped like the context
// itself, which EF Core registers per request; QueryService is scoped too, since it depends on one.
builder.Services.AddSingleton<IChunkingService, ChunkingService>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<ICompletionService, CompletionService>();
builder.Services.AddScoped<IVectorStoreService, PgVectorStoreService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IQueryService, QueryService>();

var connectionString = builder.Configuration.GetConnectionString("DocMind") ??
    throw new InvalidOperationException("Connection string 'DocMind' is not configured");

builder.Services.AddDocMindData(connectionString);

// Stores users through DocMindDbContext and registers both authentication schemes that
// MapIdentityApi's /login can issue: an HttpOnly cookie (what the UI uses, via
// ?useCookies=true) and a bearer token. Unauthenticated requests get a 401, not a redirect.
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<DocMindDbContext>();

var app = builder.Build();

_ = app.UseApiExceptionHandling();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    _ = app.MapOpenApi();
    _ = app.MapScalarApiReference();
    _ = app.UseCors(angularDevCorsPolicy);

    // Brings the local database up to date on startup so `docker compose up -d` plus
    // `dotnet run` is all a developer needs. Outside Development, migrations are applied
    // as an explicit deployment step instead.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DocMindDbContext>().Database.MigrateAsync();
}

_ = app.UseHttpsRedirection();

// Called explicitly so they run after UseCors above. If left to ASP.NET Core they would be
// added implicitly at the start of the pipeline, ahead of CORS, and 401 responses would go out
// without CORS headers — the browser would report a network error instead of a 401.
_ = app.UseAuthentication();
_ = app.UseAuthorization();

var auth = app.MapGroup("/auth").WithTags("Auth");

// Provides /auth/register, /auth/login, /auth/manage/info and the rest of Identity's endpoints.
_ = auth.MapIdentityApi<ApplicationUser>();

// MapIdentityApi has no logout: bearer tokens can't be revoked server-side. For the cookie
// session the UI uses, signing out just expires the cookie.
_ = auth.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.NoContent();
})
.RequireAuthorization()
.WithName("Logout")
.WithSummary("Signs the current user out")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status401Unauthorized);

var documents = app.MapGroup("/documents").WithTags("Documents").RequireAuthorization();

_ = documents.MapPost("/upload", async (HttpRequest request, ClaimsPrincipal user, IDocumentService documentService) =>
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
    var documentId = await documentService.IndexDocumentAsync(user.GetUserId(), stream, file.FileName);

    return Results.Ok(new UploadDocumentResponse(documentId, file.FileName, "Document indexed successfully."));
})
.Accepts<IFormFile>("multipart/form-data")
.WithName("UploadDocument")
.WithSummary("Uploads and indexes a PDF document")
.WithDescription("Extracts text from the uploaded PDF (25 MB max), splits it into chunks, generates embeddings for each chunk, and stores the document, the file and the chunks for the signed-in user. Uploading the same file twice returns 409.")
.Produces<UploadDocumentResponse>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces<ErrorResponse>(StatusCodes.Status409Conflict)
.Produces<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)
.Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

_ = documents.MapGet(string.Empty, async (ClaimsPrincipal user, IDocumentService documentService) =>
{
    var summaries = await documentService.ListDocumentsAsync(user.GetUserId());

    return Results.Ok(summaries
        .Select(summary => new DocumentResponse(summary.Id, summary.FileName, summary.SizeBytes, summary.CreatedAt))
        .ToList());
})
.WithName("ListDocuments")
.WithSummary("Lists the signed-in user's documents, newest first")
.Produces<List<DocumentResponse>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

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

app.MapPost("/query", async (QueryRequest request, ClaimsPrincipal user, IQueryService queryService) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new ErrorResponse("Question must not be null or empty."));
    }

    var topK = request.TopK ?? 5;
    var result = await queryService.AskAsync(user.GetUserId(), request.Question, topK);

    var sources = result.Sources
        .Select(scored => new SourceResult(
            scored.Chunk.DocumentId,
            scored.Chunk.SequenceNumber,
            scored.Score,
            BuildExcerpt(scored.Chunk.Content)))
        .ToList();

    return Results.Ok(new QueryResponse(result.Answer, sources));
})
.WithName("Query")
.RequireAuthorization()
.WithSummary("Answers a question using retrieval-augmented generation over indexed documents")
.WithDescription("Embeds the question, retrieves the most similar chunks from the signed-in user's documents, and asks the completion model to answer using only that context. Returns an empty source list and a natural 'no documents indexed yet' answer if the user has no documents.")
.Produces<QueryResponse>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

app.Run();

static string BuildExcerpt(string content)
{
    const int maxLength = 150;
    return content.Length <= maxLength ? content : string.Concat(content.AsSpan(0, maxLength), "...");
}

// Exposes the otherwise-internal top-level Program class so DocMind.Tests can boot the real
// app via WebApplicationFactory<Program> for integration tests.
public partial class Program
{
}
