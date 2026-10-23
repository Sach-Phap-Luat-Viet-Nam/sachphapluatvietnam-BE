using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Categories;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.Property(category => category.Name).HasMaxLength(255);
        builder.Property(category => category.Slug).HasMaxLength(Slug.MaxLength);

        // Slug chỉ cần duy nhất giữa các danh mục chưa xóa, để tạo lại được danh mục cùng slug.
        builder.HasIndex(category => category.Slug).IsUnique().HasFilter("deleted_at IS NULL");

        builder.HasOne(category => category.Parent)
            .WithMany()
            .HasForeignKey(category => category.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(category => category.DeletedAt == null);
    }
}
