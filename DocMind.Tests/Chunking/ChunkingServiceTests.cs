using System.Text;
using DocMind.Core.Chunking;
using Microsoft.ML.Tokenizers;

namespace DocMind.Tests.Chunking;

public class ChunkingServiceTests
{
    private static readonly TiktokenTokenizer Tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");

    [Fact]
    public void ChunkText_ShortText_ReturnsSingleChunk()
    {
        var service = new ChunkingService();
        var text = BuildTextWithExactTokenCount(100);

        var chunks = service.ChunkText(text, "doc-short");

        var chunk = Assert.Single(chunks);
        Assert.Equal("doc-short", chunk.DocumentId);
        Assert.Equal(0, chunk.SequenceNumber);
        Assert.Equal(100, chunk.TokenCount);
    }

    [Fact]
    public void ChunkText_LongText_ConsecutiveChunksOverlapByConfiguredTokenCount()
    {
        const int overlapTokens = 75;
        var service = new ChunkingService(chunkSizeTokens: 500, overlapTokens: overlapTokens, minChunkTokens: 50);
        var text = BuildTextWithExactTokenCount(1200);

        var chunks = service.ChunkText(text, "doc-long");

        Assert.True(chunks.Count > 1, "Expected more than one chunk for a 1200-token document.");

        for (var i = 0; i < chunks.Count - 1; i++)
        {
            var currentIds = Tokenizer.EncodeToIds(chunks[i].Content);
            var nextIds = Tokenizer.EncodeToIds(chunks[i + 1].Content);

            var tailOfCurrent = currentIds.Skip(currentIds.Count - overlapTokens).ToArray();
            var headOfNext = nextIds.Take(overlapTokens).ToArray();

            Assert.Equal(tailOfCurrent, headOfNext);
        }
    }

    [Fact]
    public void ChunkText_TrailingChunkBelowMinimum_IsMergedIntoPreviousChunk()
    {
        // chunkSize=10, overlap=3, step=7 -> ranges (0,10) then (7,total).
        // With total=11 the trailing range is (7,11), 4 tokens, below minChunkTokens=5,
        // so it must be folded into the previous chunk instead of surviving as its own chunk.
        var service = new ChunkingService(chunkSizeTokens: 10, overlapTokens: 3, minChunkTokens: 5);
        var text = BuildTextWithExactTokenCount(11);

        var chunks = service.ChunkText(text, "doc-merge");

        var chunk = Assert.Single(chunks);
        Assert.Equal(0, chunk.SequenceNumber);
        Assert.Equal(11, chunk.TokenCount);
        Assert.Equal(11, Tokenizer.CountTokens(chunk.Content));
    }

    private static string BuildTextWithExactTokenCount(int tokenCount)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < tokenCount; i++)
        {
            sb.Append(i == 0 ? "cat" : " cat");
            var actualCount = Tokenizer.CountTokens(sb.ToString());
            if (actualCount != i + 1)
            {
                throw new InvalidOperationException(
                    $"Tokenizer produced {actualCount} tokens after {i + 1} words; test assumption of one token per word no longer holds.");
            }
        }

        return sb.ToString();
    }
}
