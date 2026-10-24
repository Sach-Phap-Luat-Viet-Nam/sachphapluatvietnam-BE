namespace SachLuat.SharedKernel.Persistence;

/// <summary>Mẫu cho <c>EF.Functions.ILike</c> khi tìm theo chuỗi người dùng nhập.</summary>
public static class LikePatterns
{
    public const string EscapeCharacter = @"\";

    /// <summary>Mẫu ILIKE "chứa chuỗi", escape <c>%</c> và <c>_</c> để người dùng không chèn wildcard.</summary>
    public static string Contains(string term) => "%" + term
        .Replace(@"\", @"\\", StringComparison.Ordinal)
        .Replace("%", @"\%", StringComparison.Ordinal)
        .Replace("_", @"\_", StringComparison.Ordinal) + "%";
}
