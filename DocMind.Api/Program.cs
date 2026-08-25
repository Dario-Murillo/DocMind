using DocMind.Api;
using DocMind.Api.Contracts;
using DocMind.Core.Chunking;
using DocMind.Core.Completion;
using DocMind.Core.Documents;
using DocMind.Core.Embeddings;
using DocMind.Core.Query;
using DocMind.Core.VectorStore;
using Scalar.AspNetCore;

const string angularDevCorsPolicy = "AngularDev";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Lets the Angular dev server (ng serve, default port 4200) call this API directly during
// local development. Only registered in Development further down — see IsDevelopment() below.
builder.Services.AddCors(options => options.AddPolicy(angularDevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()));

// All of DocumentService's and QueryService's own dependencies are singletons (the chunking
// tokenizer, the Ollama-backed embedding/completion clients, and the shared in-memory vector
// store), and neither service holds any per-request state itself, so registering them as
// singletons too avoids pointless per-request allocations without changing behavior.
builder.Services.AddSingleton<IChunkingService, ChunkingService>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IVectorStoreService, InMemoryVectorStoreService>();
builder.Services.AddSingleton<IDocumentService, DocumentService>();
builder.Services.AddSingleton<ICompletionService, CompletionService>();
builder.Services.AddSingleton<IQueryService, QueryService>();

var app = builder.Build();

_ = app.UseApiExceptionHandling();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    _ = app.MapOpenApi();
    _ = app.MapScalarApiReference();
    _ = app.UseCors(angularDevCorsPolicy);
}

_ = app.UseHttpsRedirection();

app.MapPost("/documents/upload", async (HttpRequest request, IDocumentService documentService) =>
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
    var documentId = await documentService.IndexDocumentAsync(stream, file.FileName);

    return Results.Ok(new UploadDocumentResponse(documentId, file.FileName, "Document indexed successfully."));
})
.Accepts<IFormFile>("multipart/form-data")
.WithName("UploadDocument")
.WithSummary("Uploads and indexes a PDF document")
.WithDescription("Extracts text from the uploaded PDF, splits it into chunks, generates embeddings for each chunk, and stores them so /query can retrieve them later.")
.Produces<UploadDocumentResponse>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)
.Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

app.MapPost("/query", async (QueryRequest request, IQueryService queryService) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new ErrorResponse("Question must not be null or empty."));
    }

    var topK = request.TopK ?? 5;
    var result = await queryService.AskAsync(request.Question, topK);

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
.WithSummary("Answers a question using retrieval-augmented generation over indexed documents")
.WithDescription("Embeds the question, retrieves the most relevant chunks from the vector store, and asks the completion model to answer using only that context. Returns an empty source list and a natural 'no documents indexed yet' answer if the vector store is empty.")
.Produces<QueryResponse>(StatusCodes.Status200OK)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
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
