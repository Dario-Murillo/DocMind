namespace DocMind.Core.Completion;


using DocMind.Core.Chunking;

public interface ICompletionService
{
    public Task<string> GenerateAnswerAsync(string question, List<Chunk> contextChunks);
}
