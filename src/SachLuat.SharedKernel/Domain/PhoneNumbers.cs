namespace SachLuat.SharedKernel.Domain;

/// <summary>Chuẩn hóa số điện thoại Việt Nam để lưu và so khớp giữa các module.</summary>
public static class PhoneNumbers
{
    /// <summary>
    /// Chỉ giữ chữ số, đổi tiền tố quốc gia <c>84</c> thành <c>0</c>: <c>0901234567</c>, <c>+84901234567</c> và
    /// <c>84901234567</c> đều thành <c>0901234567</c>. Không có chữ số nào thì trả chuỗi rỗng.
    /// </summary>
    public static string Normalize(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11
            ? "0" + digits[2..]
            : digits;
    }
}
