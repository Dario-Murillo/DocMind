namespace DocMind.Core.Data;

using DocMind.Core.Users;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// IdentityUserContext rather than IdentityDbContext: DocMind has no roles, so this skips the
// AspNetRoles, AspNetUserRoles and AspNetRoleClaims tables. Switching the base class (plus a
// migration) brings them back if roles are ever needed.
public class DocMindDbContext(DbContextOptions<DocMindDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        _ = builder.HasPostgresExtension("vector");
    }

}