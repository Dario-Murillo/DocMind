# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

DocMind is a RAG (Retrieval-Augmented Generation) document Q&A system built on .NET 9. It is a solution of three projects:

- **DocMind.Api** — ASP.NET Core Minimal API (entry point, HTTP endpoints). Targets `Microsoft.NET.Sdk.Web`.
- **DocMind.Core** — business logic (document ingestion, chunking, embeddings, retrieval, RAG pipeline). Targets `Microsoft.NET.Sdk`, referenced by both Api and Tests.
- **DocMind.Tests** — xUnit test project, references DocMind.Core.

The project is currently a bare scaffold (default template files, no domain code yet). The intended stack, not yet wired up:

- **Semantic Kernel** for generating embeddings and calling OpenAI (chat completion).
- **PdfPig** for extracting text from PDF documents.

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
