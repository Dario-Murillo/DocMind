using DocMind.Core.Chunking;

namespace DocMind.Core.VectorStore;

public record ScoredChunk(Chunk Chunk, float Score);
