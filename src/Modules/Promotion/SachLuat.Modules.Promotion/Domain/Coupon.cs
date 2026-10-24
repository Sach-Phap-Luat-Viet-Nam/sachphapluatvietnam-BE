using System.Text.RegularExpressions;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Promotion.Domain;

/// <summary>Mã giảm giá thuộc một chương trình. Một mã dùng chung hoặc nhiều mã dùng một lần sinh hàng loạt.</summary>
internal sealed class Coupon : AuditableEntity
{
    public const int CodeMaxLength = 40;

    public Coupon(long promotionId, string code, int? usageLimit)
    {
        PromotionId = promotionId;
        Code = CouponCodes.Normalize(code);
        if (!CouponCodes.IsValid(Code))
        {
            throw PromotionErrors.Invalid("Mã giảm giá gồm 4–40 ký tự chữ, số, gạch nối hoặc gạch dưới");
        }

        SetLimit(usageLimit);
        IsActive = true;
    }

    // EF Core
    private Coupon()
    {
    }

    public long PromotionId { get; private set; }

    /// <summary>Chữ in hoa, duy nhất toàn hệ thống.</summary>
    public string Code { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    /// <summary>null: dùng đến hết lượt của chương trình.</summary>
    public int? UsageLimit { get; private set; }

    public int UsedCount { get; private set; }

    public bool IsExhausted => UsageLimit <= UsedCount;

    public void SetLimit(int? usageLimit)
    {
        if (usageLimit is <= 0)
        {
            throw PromotionErrors.Invalid("Giới hạn lượt dùng phải lớn hơn 0");
        }

        UsageLimit = usageLimit;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void IncrementUsage() => UsedCount++;

    public void DecrementUsage() => UsedCount = Math.Max(0, UsedCount - 1);
}

internal static partial class CouponCodes
{
    // Bỏ 0/O, 1/I/L để khách chép mã không nhầm.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string normalizedCode) => CodePattern().IsMatch(normalizedCode);

    public static string Generate(string prefix, int length = 8)
    {
        var chars = System.Security.Cryptography.RandomNumberGenerator.GetItems<char>(Alphabet, length);
        return prefix + new string(chars);
    }

    [GeneratedRegex("^[A-Z0-9_-]{4,40}$")]
    private static partial Regex CodePattern();
}
