namespace DocMind.Core.Chunking;


using Microsoft.ML.Tokenizers;

public class ChunkingService : IChunkingService
{
    private const int DefaultChunkSizeTokens = 500;
    private const int DefaultOverlapTokens = 75;
    private const int DefaultMinChunkTokens = 50;

    private readonly TiktokenTokenizer tokenizer;
    private readonly int chunkSizeTokens;
    private readonly int overlapTokens;
    private readonly int minChunkTokens;

    public ChunkingService(
        int chunkSizeTokens = DefaultChunkSizeTokens,
        int overlapTokens = DefaultOverlapTokens,
        int minChunkTokens = DefaultMinChunkTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSizeTokens);
        if (overlapTokens < 0 || overlapTokens >= chunkSizeTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapTokens));
        }

        this.chunkSizeTokens = chunkSizeTokens;
        this.overlapTokens = overlapTokens;
        this.minChunkTokens = minChunkTokens;
        this.tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");
    }

    public List<Chunk> ChunkText(string text, string sourceDocumentId)
    {
        var ids = this.tokenizer.EncodeToIds(text);
        var totalTokens = ids.Count;

        var chunks = new List<Chunk>();
        if (totalTokens == 0)
        {
            return chunks;
        }

        var ranges = this.BuildTokenRanges(totalTokens);
        this.MergeOrDiscardTrailingSmallChunk(ranges);

        for (var sequenceNumber = 0; sequenceNumber < ranges.Count; sequenceNumber++)
        {
            var (start, end) = ranges[sequenceNumber];
            var tokenSlice = ids.Skip(start).Take(end - start).ToArray();
            var content = this.tokenizer.Decode(tokenSlice);

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
        var step = this.chunkSizeTokens - this.overlapTokens;
        var ranges = new List<(int Start, int End)>();

        var start = 0;
        while (true)
        {
            var end = Math.Min(start + this.chunkSizeTokens, totalTokens);
            ranges.Add((start, end));

            if (end >= totalTokens)
            {
                break;
            }

            start += step;
        }

        return ranges;
    }

    private void MergeOrDiscardTrailingSmallChunk(List<(int Start, int End)> ranges)
    {
        if (ranges.Count <= 1)
        {
            return;
        }

        var (lastStart, lastEnd) = ranges[^1];
        if (lastEnd - lastStart >= this.minChunkTokens)
        {
            return;
        }

        ranges.RemoveAt(ranges.Count - 1);

        var (previousStart, _) = ranges[^1];
        ranges[^1] = (previousStart, lastEnd);
    }
}
