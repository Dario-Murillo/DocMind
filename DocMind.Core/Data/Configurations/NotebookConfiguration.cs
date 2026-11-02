namespace DocMind.Core.Data.Configurations;

using DocMind.Core.Notebooks;
using DocMind.Core.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class NotebookConfiguration : IEntityTypeConfiguration<Notebook>
{
    public void Configure(EntityTypeBuilder<Notebook> builder)
    {
        _ = builder.Property(notebook => notebook.Title).HasMaxLength(Notebook.MaxTitleLength);

        // Serves "list this user's notebooks, newest first". UserId leads the index, so EF doesn't
        // create a separate index for the UserId foreign key.
        _ = builder.HasIndex(notebook => new { notebook.UserId, notebook.CreatedAt });

        _ = builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(notebook => notebook.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
