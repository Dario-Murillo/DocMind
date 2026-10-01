# DocMind

A local, self-hosted RAG (Retrieval-Augmented Generation) document Q&A system. Upload a PDF, then ask questions about it — answered using only the content you indexed, no external/paid APIs involved.

- **DocMind.Api** / **DocMind.Core** — .NET 9 backend (ASP.NET Core Minimal API + business logic). Chunks and embeds documents, retrieves relevant passages, and generates answers via a local [Ollama](https://ollama.com) instance (`nomic-embed-text` for embeddings, `llama3.1` for chat).
- **DocMind.UI** — Angular 22 frontend for uploading documents and asking questions.
- **DocMind.Tests** — xUnit test suite for the backend.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org) / npm (for the UI)
- [Docker](https://www.docker.com/products/docker-desktop/) (runs PostgreSQL with pgvector, and the test database)
- [Ollama](https://ollama.com) installed and running, with the required models pulled:

  ```bash
  ollama pull nomic-embed-text
  ollama pull llama3.1
  ```

## Getting started

Start the database and run the backend from the repository root (where `DocMind.sln` lives):

```bash
docker compose up -d
dotnet tool restore
dotnet restore
dotnet run --project DocMind.Api
```

PostgreSQL is exposed on host port `5433`. In development the API applies pending database migrations on startup.

The API listens on `http://localhost:5276` in development, with interactive docs at `/scalar/v1`.

Run the frontend from `DocMind.UI/` (in a separate terminal):

```bash
cd DocMind.UI
npm install
npm start
```

The UI is served at `http://localhost:4200` and talks to the API above.

## Running tests

```bash
dotnet test
```

Docker must be running: the API tests start a disposable PostgreSQL container through Testcontainers, so they never touch the development database. Tests tagged `Category=Integration` also need Ollama running.

For more detail — project conventions, architecture decisions, and the full command reference — see [CLAUDE.md](CLAUDE.md).
