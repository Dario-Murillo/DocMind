namespace DocMind.Core.Conversations;

// A chunk an answer was based on. FileName and Excerpt are copies taken when the question was
// asked, so the answer still shows where it came from after the document is deleted; DocumentId
// then becomes null.
public class MessageSource
{
    public Guid Id { get; set; }

    public Guid MessageId { get; set; }

    public Guid? DocumentId { get; set; }

    public required string FileName { get; set; }

    public int SequenceNumber { get; set; }

    public float Score { get; set; }

    public required string Excerpt { get; set; }
}
