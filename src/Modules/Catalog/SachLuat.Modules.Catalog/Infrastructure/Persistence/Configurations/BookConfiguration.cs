using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Authors;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Categories;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");

        builder.Property(book => book.Title).HasMaxLength(255);
        builder.Property(book => book.Slug).HasMaxLength(Slug.MaxLength);
        builder.HasIndex(book => book.Slug).IsUnique();
        builder.Property(book => book.MetaTitle).HasMaxLength(255);
        builder.Property(book => book.MetaDescription).HasMaxLength(500);
        builder.HasIndex(book => book.Status);

        builder.PrimitiveCollection(book => book.TargetAudiences)
            .ElementType(element => element
                .HasConversion<SnakeCaseEnumConverter<TargetAudience>>()
                .HasMaxLength(SnakeCaseEnumConverter<TargetAudience>.MaxLength));

        builder.HasMany(book => book.Editions)
            .WithOne()
            .HasForeignKey(edition => edition.BookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(book => book.Categories)
            .WithMany()
            .UsingEntity(
                "book_categories",
                right => right.HasOne(typeof(Category)).WithMany().HasForeignKey("category_id"),
                left => left.HasOne(typeof(Book)).WithMany().HasForeignKey("book_id"),
                join => join.HasKey("book_id", "category_id"));

        builder.HasMany(book => book.Authors)
            .WithMany()
            .UsingEntity(
                "book_authors",
                right => right.HasOne(typeof(Author)).WithMany().HasForeignKey("author_id"),
                left => left.HasOne(typeof(Book)).WithMany().HasForeignKey("book_id"),
                join => join.HasKey("book_id", "author_id"));

        builder.Ignore(book => book.LatestEdition);
    }
}
