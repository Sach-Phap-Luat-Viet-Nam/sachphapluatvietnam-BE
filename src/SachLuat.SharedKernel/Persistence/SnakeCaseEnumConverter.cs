using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SachLuat.SharedKernel.Persistence;

/// <summary>
/// Lưu enum dạng chuỗi snake_case (<c>OnSale</c> → <c>on_sale</c>) để khớp giá trị trong thiết kế dữ liệu.
/// Đổi tên member enum là đổi dữ liệu trong DB: cần migration cập nhật giá trị cũ.
/// </summary>
public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => ToProvider(value),
    value => FromProvider(value))
    where TEnum : struct, Enum
{
    public const int MaxLength = 30;

    private static readonly Dictionary<TEnum, string> ToProviderMap = Enum.GetValues<TEnum>()
        .ToDictionary(value => value, value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()));

    private static readonly Dictionary<string, TEnum> FromProviderMap = ToProviderMap
        .ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public static string ToProvider(TEnum value) => ToProviderMap.TryGetValue(value, out var text)
        ? text
        : throw new InvalidOperationException($"Value '{value}' is not defined in enum {typeof(TEnum).Name}.");

    public static TEnum FromProvider(string value) => FromProviderMap.TryGetValue(value, out var result)
        ? result
        : throw new InvalidOperationException($"Database value '{value}' is not valid for enum {typeof(TEnum).Name}.");

    /// <summary>Đọc giá trị snake_case từ query string (model binding mặc định không hiểu <c>on_sale</c>).</summary>
    public static bool TryParse(string? value, out TEnum result)
    {
        result = default;
        return value is not null && FromProviderMap.TryGetValue(value, out result);
    }
}
