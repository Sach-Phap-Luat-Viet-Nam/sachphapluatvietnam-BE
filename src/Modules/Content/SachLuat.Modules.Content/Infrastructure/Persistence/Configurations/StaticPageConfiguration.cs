using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Content.Application;
using SachLuat.Modules.Content.Domain;

namespace SachLuat.Modules.Content.Infrastructure.Persistence.Configurations;

internal sealed class StaticPageConfiguration : IEntityTypeConfiguration<StaticPage>
{
    public void Configure(EntityTypeBuilder<StaticPage> builder)
    {
        builder.ToTable("static_pages");

        builder.Property(page => page.Slug).HasMaxLength(StaticPage.SlugMaxLength);
        builder.Property(page => page.Title).HasMaxLength(StaticPage.TitleMaxLength);
        builder.Property(page => page.Description).HasMaxLength(StaticPage.DescriptionMaxLength);
        builder.Property(page => page.ContentHtml).HasColumnType("text");

        builder.HasIndex(page => page.Slug).IsUnique().HasDatabaseName(StaticPageService.SlugIndex);
    }
}
