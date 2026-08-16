namespace DocMind.Core.Chunking;

public interface IChunkingService
{
    List<Chunk> ChunkText(string text, string sourceDocumentId);
}
