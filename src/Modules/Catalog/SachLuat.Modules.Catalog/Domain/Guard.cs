namespace SachLuat.Modules.Catalog.Domain;

internal static class Guard
{
    public static string Required(string? value, string fieldLabel) =>
        string.IsNullOrWhiteSpace(value) ? throw CatalogErrors.Required(fieldLabel) : value.Trim();

    public static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string ValidSlug(string? slug) =>
        Slug.IsValid(slug) ? slug! : throw CatalogErrors.SlugInvalid();
}
