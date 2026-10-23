using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Content.Domain;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Content.Infrastructure.Persistence;

internal sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public const string Schema = "content";

    public DbSet<Popup> Popups => Set<Popup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<PopupDisplayFrequency>()
            .HaveConversion<SnakeCaseEnumConverter<PopupDisplayFrequency>>()
            .HaveMaxLength(SnakeCaseEnumConverter<PopupDisplayFrequency>.MaxLength);
}
