using DocMind.Core.Chunking;

namespace DocMind.Core.Completion;

public interface ICompletionService
{
    Task<string> GenerateAnswerAsync(string question, List<Chunk> contextChunks);
}
