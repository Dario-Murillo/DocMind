# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

DocMind is a RAG (Retrieval-Augmented Generation) document Q&A system built on .NET 9. It is a solution of three projects:

- **DocMind.Api** — ASP.NET Core Minimal API (entry point, HTTP endpoints). Targets `Microsoft.NET.Sdk.Web`.
- **DocMind.Core** — business logic (document ingestion, chunking, embeddings, retrieval, RAG pipeline). Targets `Microsoft.NET.Sdk`, referenced by both Api and Tests.
- **DocMind.Tests** — xUnit test project, references DocMind.Core.

The project is currently a bare scaffold (default template files, no domain code yet). The intended stack, not yet wired up:

- **Semantic Kernel** for generating embeddings and chat completion, both via Ollama (local, no paid external dependencies — the project is 100% self-hosted):
  - **Embeddings**: Ollama local, model `nomic-embed-text`. Implemented via OllamaSharp + Microsoft.Extensions.AI (`IEmbeddingGenerator<string, Embedding<float>>`), not Semantic Kernel directly. The `Microsoft.SemanticKernel.Connectors.Ollama` package deprecated its classic interface (`ITextEmbeddingGenerationService`) in favor of this shared abstraction, so the same underlying type (`OllamaApiClient`) is used without the Kernel/IServiceCollection layer.
  - **Completion/Chat**: Ollama local, model `llama3.1`.
  - Both served at `http://localhost:11434`.
- **PdfPig** for extracting text from PDF documents.

NuGet packages: `Microsoft.SemanticKernel.Connectors.Ollama` (no `Connectors.OpenAI`).

As this scaffold is built out, prefer putting orchestration/business logic (chunking, embedding generation, retrieval, prompt construction) in DocMind.Core, keeping DocMind.Api as a thin HTTP layer over it.

## Commands

Run from the repository root (where `DocMind.sln` lives).

```bash
# Restore
dotnet restore

# Build
dotnet build

# Run the API (hot-reload during development)
dotnet run --project DocMind.Api
dotnet watch --project DocMind.Api run

# Run all tests
dotnet test

# Run a single test project
dotnet test DocMind.Tests

# Run a single test by fully-qualified name
dotnet test --filter "FullyQualifiedName~DocMind.Tests.ClassName.MethodName"

# Run tests matching a name substring
dotnet test --filter "DisplayName~SomeMethod"
```

## C# conventions

- Target framework: `net9.0`, with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` across all projects — write nullable-aware code and rely on implicit global usings rather than re-adding common `using` directives.
- New cross-project code (chunking, embedding, retrieval, PDF parsing) belongs in DocMind.Core; DocMind.Api should only wire up minimal-API endpoints and DI, delegating to DocMind.Core services.
- DocMind.Tests uses xUnit with the `Using Include="Xunit"` implicit global using already configured — no need to add `using Xunit;` per file.
- The chunking tokenizer uses the `cl100k_base` encoding as a reference standard, even though the real models (`nomic-embed-text` and `llama3.1` via Ollama) have their own internal tokenizer — this is a deliberate simplification to keep chunk sizing consistent without adding the extra complexity of a model-specific tokenizer.

## Technical decisions

- **Embeddings: OllamaSharp + Microsoft.Extensions.AI instead of Semantic Kernel directly.** The `Microsoft.SemanticKernel.Connectors.Ollama` package (prerelease) marks its dedicated class `OllamaTextEmbeddingGenerationService` and the `AddOllamaTextEmbeddingGeneration` extension method as `[Obsolete]`, pointing to `AddOllamaEmbeddingGenerator` / `OllamaApiClient.AsEmbeddingGenerationService()` instead. That "recommended" path only makes sense if a full `IKernelBuilder` or `IServiceCollection` is registered, and under the hood it ends up resolving the same `OllamaApiClient` from OllamaSharp as the implementation of `IEmbeddingGenerator<string, Embedding<float>>` (the `Microsoft.Extensions.AI` abstraction that Semantic Kernel migrated embedding generation to). That's why `EmbeddingService` instantiates `OllamaApiClient` directly: identical runtime behavior (same HTTP client, same endpoint, same model), without the overhead of spinning up a `Kernel` just to get back the same object. The `Microsoft.SemanticKernel.Connectors.Ollama` package is still referenced in `DocMind.Core.csproj` (it pulls in `OllamaSharp` and `Microsoft.Extensions.AI.Abstractions` as transitive dependencies), but the code doesn't go through its public Kernel/DI surface.

## Local requirements

- Ollama installed and running (verify with: `ollama list`).
- Models pulled: `nomic-embed-text` and `llama3.1`.
- No API keys or external provider environment variables needed.
