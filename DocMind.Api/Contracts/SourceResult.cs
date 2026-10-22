namespace DocMind.Api.Contracts;

using DocMind.Core.Conversations;

// DocumentId is null once the cited document has been deleted. FileName and Excerpt are copies
// taken when the question was asked, so they remain.
public record SourceResult(Guid? DocumentId, string FileName, int SequenceNumber, float Score, string Excerpt)
{
    public static SourceResult From(MessageSource source) =>
        new(source.DocumentId, source.FileName, source.SequenceNumber, source.Score, source.Excerpt);
}
