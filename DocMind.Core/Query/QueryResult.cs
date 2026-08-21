namespace DocMind.Core.Query;

using DocMind.Core.VectorStore;

public record QueryResult(string Answer, List<ScoredChunk> Sources);
