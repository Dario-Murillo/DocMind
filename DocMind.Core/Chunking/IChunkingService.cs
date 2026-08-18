namespace DocMind.Core.Chunking;

public interface IChunkingService
{
    public List<Chunk> ChunkText(string text, string sourceDocumentId);
}
