namespace SachLuat.Modules.Catalog.Contracts;

/// <summary>Đọc thông tin SKU cho module khác (giỏ hàng, đơn hàng, kho). Không trả SKU không tồn tại.</summary>
public interface ICatalogSkuReader
{
    Task<IReadOnlyDictionary<long, SkuInfo>> GetSkusAsync(
        IReadOnlyCollection<long> skuIds,
        CancellationToken cancellationToken);
}

/// <param name="CoverType">Giá trị snake_case: <c>paperback</c>, <c>hardcover</c>.</param>
/// <param name="SalePrice">Giá bán hiện hành (VNĐ), đã áp lịch giá đang chạy.</param>
/// <param name="RegularSalePrice">Giá bán thường (chưa áp lịch giá); <c>SalePrice &lt; RegularSalePrice</c> nghĩa là đang chạy lịch giá.</param>
/// <param name="CategoryIds">Danh mục của đầu sách, gồm cả danh mục cha (để khuyến mãi theo danh mục cha áp cho sách ở danh mục con).</param>
/// <param name="IsSellable">Đầu sách đang hiển thị và ấn bản đang bán (<c>on_sale</c> hoặc <c>clearance</c>).</param>
public sealed record SkuInfo(
    long SkuId,
    string SkuCode,
    long BookId,
    string BookTitle,
    string BookSlug,
    long EditionId,
    string? EditionName,
    string Isbn,
    string CoverType,
    decimal ListPrice,
    decimal SalePrice,
    decimal RegularSalePrice,
    int? WeightGram,
    string? CoverImageUrl,
    bool IsLatestEdition,
    IReadOnlyList<long> CategoryIds,
    bool IsSellable);
