namespace SachLuat.Modules.Catalog.Application;

internal static class LikePatterns
{
    public const string EscapeCharacter = @"\";

    /// <summary>Mẫu ILIKE "chứa chuỗi", escape <c>%</c> và <c>_</c> để người dùng không chèn wildcard.</summary>
    public static string Contains(string term) => "%" + term
        .Replace(@"\", @"\\", StringComparison.Ordinal)
        .Replace("%", @"\%", StringComparison.Ordinal)
        .Replace("_", @"\_", StringComparison.Ordinal) + "%";
}
