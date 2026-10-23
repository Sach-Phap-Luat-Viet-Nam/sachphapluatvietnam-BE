using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SachLuat.Modules.Content.Domain;

namespace SachLuat.Modules.Content.Infrastructure.Persistence.Configurations;

internal sealed class PopupConfiguration : IEntityTypeConfiguration<Popup>
{
    public void Configure(EntityTypeBuilder<Popup> builder)
    {
        builder.ToTable("popups");

        builder.Property(popup => popup.Title).HasMaxLength(Popup.TitleMaxLength);
        builder.Property(popup => popup.ImageUrl).HasMaxLength(Popup.UrlMaxLength);
        builder.Property(popup => popup.LinkUrl).HasMaxLength(Popup.UrlMaxLength);

        builder.HasQueryFilter(popup => popup.DeletedAt == null);
    }
}
