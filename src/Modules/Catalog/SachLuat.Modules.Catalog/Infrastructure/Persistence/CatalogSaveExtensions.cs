using Microsoft.EntityFrameworkCore;
using Npgsql;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Catalog.Infrastructure.Persistence;

internal static class CatalogSaveExtensions
{
    // Tên index do EFCore.NamingConventions sinh, xem migration CreateCatalogSchema.
    private static readonly Dictionary<string, Func<BusinessException>> UniqueViolations = new(StringComparer.Ordinal)
    {
        ["ix_books_slug"] = CatalogErrors.SlugDuplicate,
        ["ix_categories_slug"] = CatalogErrors.SlugDuplicate,
        ["ix_authors_slug"] = CatalogErrors.SlugDuplicate,
        ["ix_editions_isbn"] = CatalogErrors.IsbnDuplicate,
        ["ix_skus_sku_code"] = CatalogErrors.SkuCodeDuplicate,
        ["ix_skus_edition_id_cover_type"] = CatalogErrors.SkuCoverDuplicate,
    };

    /// <summary>Lưu và chuyển vi phạm unique index thành lỗi nghiệp vụ 409 thay vì 500.</summary>
    public static async Task SaveCatalogChangesAsync(this CatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: { } constraint,
        } && UniqueViolations.TryGetValue(constraint, out var error))
        {
            throw error();
        }
    }
}
