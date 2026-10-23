using SachLuat.Modules.Catalog.Domain.LegalDocuments;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Catalog.Domain.Editions;

/// <summary>Ấn bản: một lần phát hành của đầu sách, có ISBN riêng. Tạo qua <c>Book.AddEdition</c>.</summary>
internal sealed class Edition : AuditableEntity
{
    private readonly List<Sku> _skus = [];
    private readonly List<LegalDocument> _legalDocuments = [];

    internal Edition(EditionPublishingInfo publishingInfo)
    {
        UpdatePublishingInfo(publishingInfo);
        Status = EditionStatus.Draft;
    }

    // EF Core
    private Edition()
    {
    }

    public long BookId { get; private set; }

    public string? EditionName { get; private set; }

    public string Isbn { get; private set; } = string.Empty;

    public string Publisher { get; private set; } = string.Empty;

    public string PublishingDecisionNo { get; private set; } = string.Empty;

    public string? RegistrationNo { get; private set; }

    public short? PublishYear { get; private set; }

    public string? UpdatedToText { get; private set; }

    public DateOnly? UpdatedToDate { get; private set; }

    public int? Pages { get; private set; }

    public string? Size { get; private set; }

    public int? WeightGram { get; private set; }

    public string? CoverImageUrl { get; private set; }

    public string? PreviewFileUrl { get; private set; }

    public string? TableOfContents { get; private set; }

    public DateOnly? ReleaseDate { get; private set; }

    public bool IsLatest { get; private set; }

    public EditionStatus Status { get; private set; }

    public IReadOnlyCollection<Sku> Skus => _skus;

    /// <summary>Văn bản pháp luật được cập nhật trong ấn bản này.</summary>
    public IReadOnlyCollection<LegalDocument> LegalDocuments => _legalDocuments;

    public void UpdatePublishingInfo(EditionPublishingInfo info)
    {
        Isbn = Isbn13.TryNormalize(info.Isbn, out var isbn) ? isbn : throw CatalogErrors.IsbnInvalid();
        Publisher = Guard.Required(info.Publisher, "nhà xuất bản");
        PublishingDecisionNo = Guard.Required(info.PublishingDecisionNo, "số quyết định xuất bản");
        RegistrationNo = Guard.Optional(info.RegistrationNo);
        EditionName = Guard.Optional(info.EditionName);
        PublishYear = info.PublishYear;
    }

    public void UpdateDetails(EditionDetails details)
    {
        if (details.Pages <= 0 || details.WeightGram <= 0)
        {
            throw CatalogErrors.PhysicalInfoInvalid();
        }

        UpdatedToText = Guard.Optional(details.UpdatedToText);
        UpdatedToDate = details.UpdatedToDate;
        Pages = details.Pages;
        Size = Guard.Optional(details.Size);
        WeightGram = details.WeightGram;
        CoverImageUrl = Guard.Optional(details.CoverImageUrl);
        PreviewFileUrl = Guard.Optional(details.PreviewFileUrl);
        TableOfContents = Guard.Optional(details.TableOfContents);
        ReleaseDate = details.ReleaseDate;
    }

    public void ChangeStatus(EditionStatus status) => Status = status;

    public Sku AddSku(string skuCode, CoverType coverType, decimal listPrice, decimal salePrice)
    {
        if (_skus.Any(sku => sku.CoverType == coverType))
        {
            throw CatalogErrors.SkuCoverDuplicate();
        }

        var sku = new Sku(skuCode, coverType, listPrice, salePrice);
        _skus.Add(sku);
        return sku;
    }

    public void SetLegalDocuments(IEnumerable<LegalDocument> legalDocuments)
    {
        var items = legalDocuments.Distinct().ToList();
        _legalDocuments.RemoveAll(item => !items.Contains(item));
        _legalDocuments.AddRange(items.Where(item => !_legalDocuments.Contains(item)));
    }

    // Chỉ Book được gọi để giữ bất biến BR-02.
    internal void SetLatest(bool isLatest) => IsLatest = isLatest;
}
