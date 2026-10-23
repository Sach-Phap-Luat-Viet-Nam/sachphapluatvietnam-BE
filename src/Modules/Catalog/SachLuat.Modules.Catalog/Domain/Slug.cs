using System.Text.RegularExpressions;

namespace SachLuat.Modules.Catalog.Domain;

internal static partial class Slug
{
    public const int MaxLength = 255;

    /// <summary>Chữ thường không dấu, số, nối bằng một dấu gạch: <c>bo-luat-dan-su-2026</c>.</summary>
    public static bool IsValid(string? value) =>
        value is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Pattern();
}
