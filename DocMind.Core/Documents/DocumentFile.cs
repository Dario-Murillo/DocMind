namespace DocMind.Core.Documents;

public class DocumentFile
{
    public Guid DocumentId { get; set; }

    public required byte[] Content { get; set; }
}