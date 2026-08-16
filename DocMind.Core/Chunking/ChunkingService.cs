using Microsoft.ML.Tokenizers;

namespace DocMind.Core.Chunking;

public class ChunkingService : IChunkingService
{
    private const int DefaultChunkSizeTokens = 500;
    private const int DefaultOverlapTokens = 75;
    private const int DefaultMinChunkTokens = 50;

    private readonly TiktokenTokenizer _tokenizer;
    private readonly int _chunkSizeTokens;
    private readonly int _overlapTokens;
    private readonly int _minChunkTokens;

    public ChunkingService(
        int chunkSizeTokens = DefaultChunkSizeTokens,
        int overlapTokens = DefaultOverlapTokens,
        int minChunkTokens = DefaultMinChunkTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSizeTokens);
        if (overlapTokens < 0 || overlapTokens >= chunkSizeTokens)
            throw new ArgumentOutOfRangeException(nameof(overlapTokens));

        _chunkSizeTokens = chunkSizeTokens;
        _overlapTokens = overlapTokens;
        _minChunkTokens = minChunkTokens;
        _tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");
    }

    public List<Chunk> ChunkText(string text, string sourceDocumentId)
    {
        var ids = _tokenizer.EncodeToIds(text);
        var totalTokens = ids.Count;

        var chunks = new List<Chunk>();
        if (totalTokens == 0)
            return chunks;

        var ranges = BuildTokenRanges(totalTokens);
        MergeOrDiscardTrailingSmallChunk(ranges);

        for (var sequenceNumber = 0; sequenceNumber < ranges.Count; sequenceNumber++)
        {
            var (start, end) = ranges[sequenceNumber];
            var tokenSlice = ids.Skip(start).Take(end - start).ToArray();
            var content = _tokenizer.Decode(tokenSlice);

            chunks.Add(new Chunk(
                Id: Guid.NewGuid(),
                DocumentId: sourceDocumentId,
                Content: content,
                TokenCount: tokenSlice.Length,
                SequenceNumber: sequenceNumber));
        }

        return chunks;
    }

    private List<(int Start, int End)> BuildTokenRanges(int totalTokens)
    {
        var step = _chunkSizeTokens - _overlapTokens;
        var ranges = new List<(int Start, int End)>();

        var start = 0;
        while (true)
        {
            var end = Math.Min(start + _chunkSizeTokens, totalTokens);
            ranges.Add((start, end));

            if (end >= totalTokens)
                break;

            start += step;
        }

        return ranges;
    }

    private void MergeOrDiscardTrailingSmallChunk(List<(int Start, int End)> ranges)
    {
        if (ranges.Count <= 1)
            return;

        var (lastStart, lastEnd) = ranges[^1];
        if (lastEnd - lastStart >= _minChunkTokens)
            return;

        ranges.RemoveAt(ranges.Count - 1);

        var (previousStart, _) = ranges[^1];
        ranges[^1] = (previousStart, lastEnd);
    }
}
