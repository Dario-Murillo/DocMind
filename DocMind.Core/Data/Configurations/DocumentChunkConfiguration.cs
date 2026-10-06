namespace DocMind.Core.Data.Configurations;

using DocMind.Core.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        _ = builder.Property(chunk => chunk.Embedding)
            .HasColumnType($"vector({DocumentChunk.EmbeddingDimensions})");

        _ = builder.HasIndex(chunk => new { chunk.DocumentId, chunk.SequenceNumber }).IsUnique();
        _ = builder.HasIndex(chunk => chunk.UserId);
    }
}
