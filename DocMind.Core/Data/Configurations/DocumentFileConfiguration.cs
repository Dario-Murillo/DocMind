namespace DocMind.Core.Data.Configurations;

using DocMind.Core.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DocumentFileConfiguration : IEntityTypeConfiguration<DocumentFile>
{
    public void Configure(EntityTypeBuilder<DocumentFile> builder) =>
        _ = builder.HasKey(file => file.DocumentId);
}
