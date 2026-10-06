namespace DocMind.Core.Data.Configurations;

using DocMind.Core.Documents;
using DocMind.Core.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        _ = builder.Property(document => document.FileName).HasMaxLength(255);
        _ = builder.Property(document => document.Sha256).HasMaxLength(64).IsFixedLength();
        _ = builder.Property(document => document.EmbeddingModel).HasMaxLength(100);

        // One copy of each file per user. UserId leads the index, so it also serves "list this
        // user's documents" and EF doesn't create a separate index for the UserId foreign key.
        _ = builder.HasIndex(document => new { document.UserId, document.Sha256 }).IsUnique();

        _ = builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(document => document.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasOne(document => document.File)
            .WithOne()
            .HasForeignKey<DocumentFile>(file => file.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        _ = builder.HasMany(document => document.Chunks)
            .WithOne()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
