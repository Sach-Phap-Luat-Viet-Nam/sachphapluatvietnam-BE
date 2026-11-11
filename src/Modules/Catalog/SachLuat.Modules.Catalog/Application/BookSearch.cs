using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Catalog.Application;

/// <summary>Tìm đầu sách theo từ khóa <c>q</c> (FR-11), dùng chung cho website và Admin.</summary>
internal static class BookSearch
{
    /// <summary>
    /// Khớp tên sách hoặc tên tác giả (chứa chuỗi, không phân biệt hoa/thường và dấu), mã SKU (chứa chuỗi),
    /// ISBN (đủ 13 số, cho phép gạch nối). <paramref name="matchSlug"/>: khớp nguyên slug (Admin).
    /// </summary>
    public static IQueryable<Book> MatchKeyword(this IQueryable<Book> books, string keyword, bool matchSlug = false)
    {
        var term = keyword.Trim();
        var pattern = LikePatterns.Contains(term);
        var isbn = Isbn13.TryNormalize(term, out var normalizedIsbn) ? normalizedIsbn : term;

        return books.Where(book =>
            EF.Functions.ILike(
                CatalogDbFunctions.Unaccent(book.Title),
                CatalogDbFunctions.Unaccent(pattern),
                LikePatterns.EscapeCharacter)
            || book.Authors.Any(author => EF.Functions.ILike(
                CatalogDbFunctions.Unaccent(author.Name),
                CatalogDbFunctions.Unaccent(pattern),
                LikePatterns.EscapeCharacter))
            || (matchSlug && book.Slug == term)
            // Mã SKU khớp "chứa chuỗi" để khách gõ "250" vẫn ra PL250.
            || book.Editions.Any(edition => edition.Isbn == isbn
                || edition.Skus.Any(sku => EF.Functions.ILike(sku.SkuCode, pattern, LikePatterns.EscapeCharacter))));
    }
}
