using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Catalog.Domain.LegalDocuments;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable("legal_documents");

        builder.Property(document => document.DocNumber).HasMaxLength(50);
        builder.HasIndex(document => document.DocNumber);
        builder.Property(document => document.Title).HasMaxLength(500);

        builder.HasQueryFilter(document => document.DeletedAt == null);
    }
}
