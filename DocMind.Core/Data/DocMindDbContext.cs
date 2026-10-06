namespace DocMind.Core.Data;

using DocMind.Core.Documents;
using DocMind.Core.Users;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// IdentityUserContext rather than IdentityDbContext: DocMind has no roles, so this skips the
// AspNetRoles, AspNetUserRoles and AspNetRoleClaims tables. Switching the base class (plus a
// migration) brings them back if roles are ever needed.
public class DocMindDbContext(DbContextOptions<DocMindDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<Document> Documents => this.Set<Document>();

    public DbSet<DocumentFile> DocumentFiles => this.Set<DocumentFile>();

    public DbSet<DocumentChunk> DocumentChunks => this.Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        _ = builder.HasPostgresExtension("vector");

        // Picks up every IEntityTypeConfiguration<T> in DocMind.Core (Data/Configurations/).
        _ = builder.ApplyConfigurationsFromAssembly(typeof(DocMindDbContext).Assembly);
    }
}