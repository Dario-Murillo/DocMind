using DocMind.Api;
using DocMind.Core.Chunking;
using DocMind.Core.Completion;
using DocMind.Core.Conversations;
using DocMind.Core.Data;
using DocMind.Core.Documents;
using DocMind.Core.Embeddings;
using DocMind.Core.Notebooks;
using DocMind.Core.Query;
using DocMind.Core.Users;
using DocMind.Core.VectorStore;
using Microsoft.AspNetCore.DataProtection;
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

// Where Ollama listens. In Docker, Ollama:Endpoint points at the Ollama on the host
// (host.docker.internal); when it's missing, both services default to http://localhost:11434.
var ollamaEndpoint = builder.Configuration["Ollama:Endpoint"] is { Length: > 0 } endpoint
    ? new Uri(endpoint)
    : null;

// The chunking tokenizer and the Ollama-backed embedding/completion clients hold no per-request
// state, so they are singletons. The services that use DocMindDbContext are scoped like the context
// itself, which EF Core registers per request; QueryService is scoped too, since it depends on one.
builder.Services.AddSingleton<IChunkingService, ChunkingService>();
builder.Services.AddSingleton<IEmbeddingService>(_ => new EmbeddingService(ollamaEndpoint));
builder.Services.AddSingleton<ICompletionService>(_ => new CompletionService(ollamaEndpoint));
builder.Services.AddScoped<IVectorStoreService, PgVectorStoreService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IQueryService, QueryService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<INotebookService, NotebookService>();


var connectionString = builder.Configuration.GetConnectionString("DocMind") ??
    throw new InvalidOperationException("Connection string 'DocMind' is not configured");

builder.Services.AddDocMindData(connectionString);

// Stores users through DocMindDbContext and registers both authentication schemes that
// MapIdentityApi's /login can issue: an HttpOnly cookie (what the UI uses, via
// ?useCookies=true) and a bearer token. Unauthenticated requests get a 401, not a redirect.
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<DocMindDbContext>();

// The session cookie is encrypted with Data Protection keys. Inside a container they would live
// in the container's own filesystem and be lost when it's recreated, signing everyone out.
// DataProtection:KeysPath moves them to a folder that outlives the container (a volume).
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    _ = builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

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

_ = app.MapNotebookEndpoints();
_ = app.MapDocumentEndpoints();
_ = app.MapConversationEndpoints();

app.Run();

// Exposes the otherwise-internal top-level Program class so DocMind.Tests can boot the real
// app via WebApplicationFactory<Program> for integration tests.
public partial class Program
{
}
