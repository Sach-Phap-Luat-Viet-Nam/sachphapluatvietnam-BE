using Microsoft.EntityFrameworkCore;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence;

internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    // Mỗi module sở hữu một schema riêng trong cùng database.
    public const string Schema = "catalog";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
