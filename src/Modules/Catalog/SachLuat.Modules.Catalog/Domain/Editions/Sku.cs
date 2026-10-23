using System.Text.RegularExpressions;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Catalog.Domain.Editions;

/// <summary>
/// Mã hàng (bìa mềm/cứng) của một ấn bản. Tồn kho và lô in thuộc module Inventory, tham chiếu qua <c>Id</c>.
/// </summary>
internal sealed partial class Sku : AuditableEntity
{
    public const int SkuCodeMaxLength = 50;

    // numeric(12,0): tối đa 999.999.999.999 VNĐ.
    private const decimal MaxPrice = 999_999_999_999m;

    internal Sku(string skuCode, CoverType coverType, decimal listPrice, decimal salePrice)
    {
        SkuCode = NormalizeSkuCode(skuCode);
        CoverType = coverType;
        ChangePrices(listPrice, salePrice);
    }

    // EF Core
    private Sku()
    {
    }

    public long EditionId { get; private set; }

    public string SkuCode { get; private set; } = string.Empty;

    public CoverType CoverType { get; private set; }

    /// <summary>Giá bìa (VNĐ).</summary>
    public decimal ListPrice { get; private set; }

    /// <summary>Giá bán (VNĐ), không vượt giá bìa.</summary>
    public decimal SalePrice { get; private set; }

    public bool AllowPreorder { get; private set; }

    public void ChangePrices(decimal listPrice, decimal salePrice)
    {
        if (!IsWholeVnd(listPrice) || !IsWholeVnd(salePrice) || salePrice > listPrice)
        {
            throw CatalogErrors.PriceInvalid();
        }

        ListPrice = listPrice;
        SalePrice = salePrice;
    }

    public void SetPreorder(bool allowPreorder) => AllowPreorder = allowPreorder;

    private static bool IsWholeVnd(decimal price) => price is > 0 and <= MaxPrice && decimal.Truncate(price) == price;

    private static string NormalizeSkuCode(string? skuCode)
    {
        var normalized = skuCode?.Trim().ToUpperInvariant();
        return normalized is { Length: > 0 and <= SkuCodeMaxLength } && SkuCodePattern().IsMatch(normalized)
            ? normalized
            : throw CatalogErrors.SkuCodeInvalid();
    }

    [GeneratedRegex("^[A-Z0-9]+(?:-[A-Z0-9]+)*$")]
    private static partial Regex SkuCodePattern();
}
