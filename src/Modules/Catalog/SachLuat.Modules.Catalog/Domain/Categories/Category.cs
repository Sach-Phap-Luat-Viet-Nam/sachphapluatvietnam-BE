using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Catalog.Domain.Categories;

/// <summary>Danh mục theo lĩnh vực pháp luật, dạng cây (<see cref="ParentId"/>).</summary>
internal sealed class Category : AuditableEntity, ISoftDeletable
{
    public Category(string name, string slug, Category? parent = null)
    {
        Rename(name, slug);
        MoveTo(parent);
    }

    // EF Core
    private Category()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public long? ParentId { get; private set; }

    public Category? Parent { get; private set; }

    /// <summary>Thứ tự hiển thị (kéo thả trên Admin, FR-64).</summary>
    public int SortOrder { get; private set; }

    public DateTime? DeletedAt { get; private set; }

    public void Rename(string name, string slug)
    {
        Name = Guard.Required(name, "tên danh mục");
        Slug = Guard.ValidSlug(slug);
    }

    public void MoveTo(Category? parent)
    {
        // Chặn vòng lặp: không được chọn chính nó hoặc danh mục con cháu làm cha.
        // Cần nạp sẵn chuỗi Parent của danh mục cha mới để kiểm tra đủ.
        for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, this))
            {
                throw CatalogErrors.CategoryParentInvalid();
            }
        }

        // ParentId do EF gán từ navigation (kể cả khi cha chưa lưu, Id = 0); chỉ tự xóa khi bỏ cha.
        Parent = parent;
        if (parent is null)
        {
            ParentId = null;
        }
    }

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
