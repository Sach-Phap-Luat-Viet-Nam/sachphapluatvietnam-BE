namespace SachLuat.Modules.Catalog.Infrastructure.Persistence;

/// <summary>Hàm SQL của schema <c>catalog</c>, chỉ dùng trong truy vấn LINQ to EF Core.</summary>
internal static class CatalogDbFunctions
{
    public const string UnaccentName = "f_unaccent";

    /// <summary>
    /// Bỏ dấu tiếng Việt (kể cả đ → d) bằng <c>catalog.f_unaccent</c>, bọc <c>unaccent()</c> thành IMMUTABLE để
    /// đánh index trigram được (migration <c>AddUnaccentSearch</c>).
    /// </summary>
    public static string Unaccent(string input) =>
        throw new NotSupportedException($"{nameof(Unaccent)} chỉ chạy được trong truy vấn EF Core.");
}
