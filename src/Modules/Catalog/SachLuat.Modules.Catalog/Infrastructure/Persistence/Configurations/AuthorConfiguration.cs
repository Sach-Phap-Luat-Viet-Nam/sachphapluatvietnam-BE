using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Authors;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("authors");

        builder.Property(author => author.Name).HasMaxLength(255);
        builder.Property(author => author.Slug).HasMaxLength(Slug.MaxLength);
        builder.HasIndex(author => author.Slug).IsUnique().HasFilter("deleted_at IS NULL");

        builder.HasQueryFilter(author => author.DeletedAt == null);
    }
}
