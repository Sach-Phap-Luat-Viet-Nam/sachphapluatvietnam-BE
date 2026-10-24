using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Authors;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Categories;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Domain.LegalDocuments;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence;

internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    // Mỗi module sở hữu một schema riêng trong cùng database.
    public const string Schema = "catalog";

    public DbSet<Book> Books => Set<Book>();

    public DbSet<Edition> Editions => Set<Edition>();

    public DbSet<Sku> Skus => Set<Sku>();

    public DbSet<SkuPriceSchedule> SkuPriceSchedules => Set<SkuPriceSchedule>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Author> Authors => Set<Author>();

    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        MapEnum<BookType>(configurationBuilder);
        MapEnum<BookStatus>(configurationBuilder);
        MapEnum<EditionStatus>(configurationBuilder);
        MapEnum<CoverType>(configurationBuilder);
        MapEnum<LegalDocumentType>(configurationBuilder);
    }

    private static void MapEnum<TEnum>(ModelConfigurationBuilder configurationBuilder)
        where TEnum : struct, Enum =>
        configurationBuilder.Properties<TEnum>()
            .HaveConversion<SnakeCaseEnumConverter<TEnum>>()
            .HaveMaxLength(SnakeCaseEnumConverter<TEnum>.MaxLength);
}
