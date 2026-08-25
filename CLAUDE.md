# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

DocMind is a RAG (Retrieval-Augmented Generation) document Q&A system built on .NET 9, with an Angular frontend. It is a solution of three .NET projects plus a standalone Angular app:

- **DocMind.Api** — ASP.NET Core Minimal API (entry point, HTTP endpoints). Targets `Microsoft.NET.Sdk.Web`.
- **DocMind.Core** — business logic (document ingestion, chunking, embeddings, retrieval, RAG pipeline). Targets `Microsoft.NET.Sdk`, referenced by both Api and Tests.
- **DocMind.Tests** — xUnit test project, references DocMind.Core.
- **DocMind.UI** — Angular 22 single-page app (upload a document, ask a question). Calls DocMind.Api directly from the browser; not part of the `.sln`/`dotnet` build.

The stack:

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

Run from `DocMind.UI/` (the Angular app, separate from the `.sln`):

```bash
# Install dependencies
npm install

# Run the dev server (http://localhost:4200, calls DocMind.Api on http://localhost:5276)
npm start

# Production build
npm run build

# Run tests (Vitest)
npm test
```

The API must be running (`dotnet run --project DocMind.Api`) for the UI to work — CORS is only opened for `http://localhost:4200` when DocMind.Api runs in the Development environment.

## C# conventions

- Target framework: `net9.0`, with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` across all projects — write nullable-aware code and rely on implicit global usings rather than re-adding common `using` directives.
- New cross-project code (chunking, embedding, retrieval, PDF parsing) belongs in DocMind.Core; DocMind.Api should only wire up minimal-API endpoints and DI, delegating to DocMind.Core services.
- DocMind.Tests uses xUnit with the `Using Include="Xunit"` implicit global using already configured — no need to add `using Xunit;` per file.
- The chunking tokenizer uses the `cl100k_base` encoding as a reference standard, even though the real models (`nomic-embed-text` and `llama3.1` via Ollama) have their own internal tokenizer — this is a deliberate simplification to keep chunk sizing consistent without adding the extra complexity of a model-specific tokenizer.
- This project uses the standard .editorconfig from RehanSaeed  (github.com/RehanSaeed/EditorConfig). Respect its style rules when generating new code (naming conventions, using directive organization, 
modern C# syntax preferences). If `dotnet build` shows style warnings, fix them before considering the task complete. Show me the diff before saving.

## Angular conventions (DocMind.UI)

- Angular 22, standalone components only — no `NgModule`s.
- State is held in signals (`signal()`), not RxJS subjects/`BehaviorSubject`, for component-local UI state.
- Dependency injection uses the `inject()` function, not constructor injection.
- Templates use the new control-flow syntax (`@if`, `@for`), not `*ngIf`/`*ngFor`.
- Feature code lives under `src/app/features/<feature>/`; shared/cross-feature code (the `Api` HTTP client, DTOs) lives under `src/app/core/`.
- Tests run on Vitest (the Angular CLI default), not Karma/Jasmine.
- `package.json`'s `"name"` and the `angular.json` project key stay `docmind-ui` (lowercase) even though the folder is `DocMind.UI` — npm requires package names to be lowercase.
- The repo root `.editorconfig` and `.gitignore` cover DocMind.UI too; it does not have its own.

## Technical decisions

- **Embeddings: OllamaSharp + Microsoft.Extensions.AI instead of Semantic Kernel directly.** The `Microsoft.SemanticKernel.Connectors.Ollama` package (prerelease) marks its dedicated class `OllamaTextEmbeddingGenerationService` and the `AddOllamaTextEmbeddingGeneration` extension method as `[Obsolete]`, pointing to `AddOllamaEmbeddingGenerator` / `OllamaApiClient.AsEmbeddingGenerationService()` instead. That "recommended" path only makes sense if a full `IKernelBuilder` or `IServiceCollection` is registered, and under the hood it ends up resolving the same `OllamaApiClient` from OllamaSharp as the implementation of `IEmbeddingGenerator<string, Embedding<float>>` (the `Microsoft.Extensions.AI` abstraction that Semantic Kernel migrated embedding generation to). That's why `EmbeddingService` instantiates `OllamaApiClient` directly: identical runtime behavior (same HTTP client, same endpoint, same model), without the overhead of spinning up a `Kernel` just to get back the same object. The `Microsoft.SemanticKernel.Connectors.Ollama` package is still referenced in `DocMind.Core.csproj` (it pulls in `OllamaSharp` and `Microsoft.Extensions.AI.Abstractions` as transitive dependencies), but the code doesn't go through its public Kernel/DI surface.

## Local requirements

- Ollama installed and running (verify with: `ollama list`).
- Models pulled: `nomic-embed-text` and `llama3.1`.
- No API keys or external provider environment variables needed.
- Node.js/npm (for DocMind.UI) — developed against Node 24 / npm 11, matching Angular CLI 22's requirements.
