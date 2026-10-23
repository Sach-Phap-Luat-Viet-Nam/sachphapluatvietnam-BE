using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Domain.LegalDocuments;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class EditionConfiguration : IEntityTypeConfiguration<Edition>
{
    public void Configure(EntityTypeBuilder<Edition> builder)
    {
        builder.ToTable("editions", table =>
        {
            table.HasCheckConstraint("ck_editions_isbn_format", "isbn ~ '^97[89][0-9]{10}$'");
            table.HasCheckConstraint("ck_editions_pages_positive", "pages IS NULL OR pages > 0");
            table.HasCheckConstraint("ck_editions_weight_positive", "weight_gram IS NULL OR weight_gram > 0");
        });

        builder.Property(edition => edition.EditionName).HasMaxLength(100);
        builder.Property(edition => edition.Isbn).HasMaxLength(Isbn13.Length).IsFixedLength();
        builder.HasIndex(edition => edition.Isbn).IsUnique();
        builder.Property(edition => edition.Publisher).HasMaxLength(255);
        builder.Property(edition => edition.PublishingDecisionNo).HasMaxLength(100);
        builder.Property(edition => edition.RegistrationNo).HasMaxLength(100);
        builder.Property(edition => edition.UpdatedToText).HasMaxLength(500);
        builder.Property(edition => edition.Size).HasMaxLength(50);
        builder.Property(edition => edition.CoverImageUrl).HasMaxLength(500);
        builder.Property(edition => edition.PreviewFileUrl).HasMaxLength(500);

        builder.HasIndex(edition => edition.BookId);

        // BR-02: tối đa một ấn bản mới nhất cho mỗi đầu sách, chặn cả ở tầng CSDL.
        builder.HasIndex(edition => edition.BookId, "LatestEditionPerBook")
            .HasDatabaseName("ux_editions_book_id_latest")
            .IsUnique()
            .HasFilter("is_latest");

        builder.HasMany(edition => edition.Skus)
            .WithOne()
            .HasForeignKey(sku => sku.EditionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(edition => edition.LegalDocuments)
            .WithMany()
            .UsingEntity(
                "edition_legal_documents",
                right => right.HasOne(typeof(LegalDocument)).WithMany().HasForeignKey("legal_document_id"),
                left => left.HasOne(typeof(Edition)).WithMany().HasForeignKey("edition_id"),
                join => join.HasKey("edition_id", "legal_document_id"));
    }
}
