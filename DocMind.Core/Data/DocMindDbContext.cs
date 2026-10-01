namespace DocMind.Core.Data;

using Microsoft.EntityFrameworkCore;

public class DocMindDbContext(DbContextOptions<DocMindDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
    _ = modelBuilder.HasPostgresExtension("vector");
}
