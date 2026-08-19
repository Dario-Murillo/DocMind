namespace DocMind.Core.VectorStore;


using DocMind.Core.Chunking;

public record ScoredChunk(Chunk Chunk, float Score);
