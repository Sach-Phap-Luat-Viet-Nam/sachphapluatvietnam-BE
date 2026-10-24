using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Shipping.Domain;

namespace SachLuat.Modules.Shipping.Infrastructure.Persistence;

internal sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options) : DbContext(options)
{
    public const string Schema = "shipping";

    public DbSet<ShippingZone> Zones => Set<ShippingZone>();

    public DbSet<ShippingSettings> Settings => Set<ShippingSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShippingDbContext).Assembly);
    }
}
