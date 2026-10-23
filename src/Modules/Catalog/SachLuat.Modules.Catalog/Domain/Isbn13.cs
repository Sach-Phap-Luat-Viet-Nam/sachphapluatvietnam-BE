using System.Diagnostics.CodeAnalysis;

namespace SachLuat.Modules.Catalog.Domain;

internal static class Isbn13
{
    public const int Length = 13;

    /// <summary>
    /// Chuẩn hóa (bỏ gạch nối, khoảng trắng) và kiểm tra ISBN-13: tiền tố 978/979, đúng số kiểm tra.
    /// </summary>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? isbn)
    {
        isbn = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = input.Where(c => c is not ('-' or ' ')).ToArray();
        if (digits.Length != Length || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var normalized = new string(digits);
        if (!normalized.StartsWith("978", StringComparison.Ordinal)
            && !normalized.StartsWith("979", StringComparison.Ordinal))
        {
            return false;
        }

        if (ComputeCheckDigit(normalized) != normalized[12] - '0')
        {
            return false;
        }

        isbn = normalized;
        return true;
    }

    /// <summary>Số kiểm tra từ 12 chữ số đầu: trọng số xen kẽ 1 và 3, modulo 10.</summary>
    public static int ComputeCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return (10 - (sum % 10)) % 10;
    }
}
