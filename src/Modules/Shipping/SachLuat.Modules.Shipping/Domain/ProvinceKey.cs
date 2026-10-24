using System.Globalization;
using System.Text;

namespace SachLuat.Modules.Shipping.Domain;

/// <summary>
/// Khóa so khớp tên tỉnh/thành do khách nhập hoặc FE gửi: chữ thường, bỏ dấu, bỏ tiền tố hành chính, nên
/// "Thành phố Hà Nội", "TP. Hà Nội" và "ha noi" cùng khớp một vùng.
/// </summary>
internal static class ProvinceKey
{
    public const int MaxLength = 100;

    private static readonly string[] Prefixes = ["thanh pho ", "tinh ", "tp "];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // Mọi ký tự không phải chữ/số (dấu chấm, gạch nối, khoảng trắng kép) gộp thành một khoảng trắng.
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        var key = builder.ToString().Trim() + " ";
        foreach (var prefix in Prefixes)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) && key.Length > prefix.Length)
            {
                key = key[prefix.Length..];
                break;
            }
        }

        return key.Trim();
    }
}
