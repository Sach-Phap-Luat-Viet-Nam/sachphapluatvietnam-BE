using SachLuat.Modules.Catalog.Domain.Authors;
using SachLuat.Modules.Catalog.Domain.Categories;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Catalog.Domain.Books;

/// <summary>
/// Đầu sách (aggregate root): sở hữu các ấn bản và đảm bảo tối đa một ấn bản mới nhất (BR-02).
/// Không xóa mềm: ẩn khỏi website bằng <see cref="BookStatus.Hidden"/>, vì đơn hàng cũ vẫn tham chiếu SKU.
/// </summary>
internal sealed class Book : AuditableEntity
{
    private readonly List<Edition> _editions = [];
    private readonly List<Category> _categories = [];
    private readonly List<Author> _authors = [];

    public Book(string title, string slug, BookType bookType)
    {
        Rename(title, slug);
        BookType = bookType;
        Status = BookStatus.Draft;
    }

    // EF Core
    private Book()
    {
    }

    public string Title { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public BookType BookType { get; private set; }

    public IReadOnlyList<TargetAudience> TargetAudiences { get; private set; } = [];

    public string? MetaTitle { get; private set; }

    public string? MetaDescription { get; private set; }

    public BookStatus Status { get; private set; }

    public IReadOnlyCollection<Edition> Editions => _editions;

    public IReadOnlyCollection<Category> Categories => _categories;

    public IReadOnlyCollection<Author> Authors => _authors;

    public Edition? LatestEdition => _editions.SingleOrDefault(edition => edition.IsLatest);

    public void Rename(string title, string slug)
    {
        Title = Guard.Required(title, "tên sách");
        Slug = Guard.ValidSlug(slug);
    }

    public void UpdateDetails(
        BookType bookType,
        string? description,
        IEnumerable<TargetAudience> targetAudiences,
        string? metaTitle,
        string? metaDescription)
    {
        BookType = bookType;
        Description = Guard.Optional(description);
        TargetAudiences = targetAudiences.Distinct().Order().ToList();
        MetaTitle = Guard.Optional(metaTitle);
        MetaDescription = Guard.Optional(metaDescription);
    }

    public void ChangeStatus(BookStatus status) => Status = status;

    public Edition AddEdition(EditionPublishingInfo publishingInfo)
    {
        var edition = new Edition(publishingInfo);
        if (_editions.Any(existing => existing.Isbn == edition.Isbn))
        {
            throw CatalogErrors.IsbnDuplicate();
        }

        _editions.Add(edition);
        return edition;
    }

    /// <summary>BR-02: đánh dấu ấn bản mới nhất và bỏ đánh dấu các ấn bản còn lại.</summary>
    public void SetLatestEdition(Edition edition)
    {
        if (!_editions.Contains(edition))
        {
            throw CatalogErrors.EditionNotFound();
        }

        foreach (var other in _editions)
        {
            other.SetLatest(ReferenceEquals(other, edition));
        }
    }

    public void AssignCategories(IEnumerable<Category> categories) => Replace(_categories, categories);

    public void AssignAuthors(IEnumerable<Author> authors) => Replace(_authors, authors);

    private static void Replace<T>(List<T> target, IEnumerable<T> source)
    {
        var items = source.Distinct().ToList();
        target.RemoveAll(item => !items.Contains(item));
        target.AddRange(items.Where(item => !target.Contains(item)));
    }
}
