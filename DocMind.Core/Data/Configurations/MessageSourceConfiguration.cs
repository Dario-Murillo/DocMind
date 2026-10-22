namespace DocMind.Core.Data.Configurations;

using DocMind.Core.Conversations;
using DocMind.Core.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class MessageSourceConfiguration : IEntityTypeConfiguration<MessageSource>
{
    public void Configure(EntityTypeBuilder<MessageSource> builder)
    {
        _ = builder.Property(source => source.FileName).HasMaxLength(255);

        // SetNull rather than Cascade: deleting a document keeps the sources that cited it. Like
        // the chunk cascade, this happens in the database, since DeleteDocumentAsync never loads
        // the sources.
        _ = builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(source => source.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
