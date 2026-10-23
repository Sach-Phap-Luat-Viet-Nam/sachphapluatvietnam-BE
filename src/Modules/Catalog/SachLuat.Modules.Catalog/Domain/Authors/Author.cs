using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Catalog.Domain.Authors;

/// <summary>Tác giả hoặc nhóm biên soạn.</summary>
internal sealed class Author : AuditableEntity, ISoftDeletable
{
    public Author(string name, string slug)
    {
        Rename(name, slug);
    }

    // EF Core
    private Author()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Biography { get; private set; }

    public int SortOrder { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public void Rename(string name, string slug)
    {
        Name = Guard.Required(name, "tên tác giả");
        Slug = Guard.ValidSlug(slug);
    }

    public void UpdateBiography(string? biography) => Biography = Guard.Optional(biography);

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
