using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Catalog.Application.Admin;
using SachLuat.Modules.Catalog.Application.Storefront;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.IntegrationTests.Catalog;

/// <summary>Tham số <c>q</c> khớp mã SKU (không phân biệt hoa/thường, khớp một phần mã).</summary>
public sealed class BookSearchPersistenceTests(CatalogDatabaseFixture fixture)
    : IClassFixture<CatalogDatabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task StorefrontListBooks_MatchesSkuCodeIgnoringCase()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var (book, skuCode) = await SaveBookAsync(dbContext);
        var service = new StorefrontQueryService(dbContext, new NoStock(), TimeProvider.System);

        var exact = await service.ListBooksAsync(new BooksQuery { Q = skuCode }, CancellationToken);
        var lower = await service.ListBooksAsync(
            new BooksQuery { Q = $"  {skuCode.ToLowerInvariant()} " }, CancellationToken);
        var partial = await service.ListBooksAsync(new BooksQuery { Q = skuCode[^8..^1] }, CancellationToken);
        var unrelated = await service.ListBooksAsync(new BooksQuery { Q = $"{skuCode}-KHAC" }, CancellationToken);

        exact.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
        lower.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
        partial.Items.Should().Contain(item => item.Id == book.Id);
        unrelated.Items.Should().NotContain(item => item.Id == book.Id);
    }

    [Fact]
    public async Task AdminListBooks_MatchesSkuCodeIgnoringCase()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var (book, skuCode) = await SaveBookAsync(dbContext);
        var service = new CatalogAdminService(dbContext, new NoStock(), TimeProvider.System);

        var result = await service.ListBooksAsync(
            new AdminBooksQuery { Q = skuCode.ToLowerInvariant() }, CancellationToken);

        var partial = await service.ListBooksAsync(
            new AdminBooksQuery { Q = skuCode[^8..^1].ToLowerInvariant() }, CancellationToken);

        result.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
        partial.Items.Should().Contain(item => item.Id == book.Id);
    }

    private static async Task<(Book Book, string SkuCode)> SaveBookAsync(CatalogDbContext dbContext)
    {
        var book = new Book("Bộ luật Hình sự", $"bo-luat-hinh-su-{Guid.NewGuid():N}", BookType.Code);
        book.ChangeStatus(BookStatus.Active);
        var edition = book.AddEdition(new EditionPublishingInfo(NewIsbn(), "NXB Tư pháp", "123/QĐ-NXBTP"));
        edition.ChangeStatus(EditionStatus.OnSale);
        book.SetLatestEdition(edition);
        var sku = edition.AddSku(
            $"SLVN-{Guid.NewGuid():N}"[..30].ToUpperInvariant(), CoverType.Paperback, 250_000m, 199_000m);

        dbContext.Books.Add(book);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ChangeTracker.Clear();
        return (book, sku.SkuCode);
    }

    private static CatalogDbContext GetDbContext(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

    private static string NewIsbn()
    {
        var digits = "978" + Random.Shared.NextInt64(0, 1_000_000_000).ToString("D9");
        return digits + Isbn13.ComputeCheckDigit(digits);
    }
}
