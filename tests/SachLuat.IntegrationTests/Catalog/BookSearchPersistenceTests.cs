using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Catalog.Application.Admin;
using SachLuat.Modules.Catalog.Application.Storefront;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Authors;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.IntegrationTests.Catalog;

/// <summary>
/// Tham số <c>q</c>: tên sách/tác giả không phân biệt dấu, mã SKU (khớp một phần), ISBN; lọc theo slug tác giả.
/// </summary>
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

    [Fact]
    public async Task StorefrontListBooks_MatchesTitleIgnoringDiacritics()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var suffix = NewSuffix();
        var (book, _) = await SaveBookAsync(dbContext, title: $"Bộ luật Đất đai {suffix}");
        var service = new StorefrontQueryService(dbContext, new NoStock(), TimeProvider.System);

        var unaccented = await service.ListBooksAsync(
            new BooksQuery { Q = $"bo luat dat dai {suffix}" }, CancellationToken);
        var upper = await service.ListBooksAsync(new BooksQuery { Q = $"ĐẤT ĐAI {suffix}" }, CancellationToken);
        var mixed = await service.ListBooksAsync(new BooksQuery { Q = $"Đất dai {suffix}" }, CancellationToken);

        unaccented.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
        upper.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
        mixed.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
    }

    [Fact]
    public async Task StorefrontListBooks_MatchesAuthorNameAndAuthorSlug()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var suffix = NewSuffix();
        var author = new Author($"Nguyễn Văn Đức {suffix}", $"nguyen-van-duc-{suffix}");
        dbContext.Authors.Add(author);
        var (book, _) = await SaveBookAsync(dbContext, author: author);
        var (otherBook, _) = await SaveBookAsync(dbContext);
        var service = new StorefrontQueryService(dbContext, new NoStock(), TimeProvider.System);

        var byName = await service.ListBooksAsync(new BooksQuery { Q = $"van duc {suffix}" }, CancellationToken);
        var bySlug = await service.ListBooksAsync(
            new BooksQuery { Author = $"nguyen-van-duc-{suffix}" }, CancellationToken);

        byName.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
        bySlug.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
        bySlug.Items.Should().NotContain(item => item.Id == otherBook.Id);
    }

    [Fact]
    public async Task StorefrontListBooks_IgnoresDeletedAuthor()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var suffix = NewSuffix();
        var author = new Author($"Tác giả đã xóa {suffix}", $"tac-gia-da-xoa-{suffix}");
        dbContext.Authors.Add(author);
        await SaveBookAsync(dbContext, author: author);
        dbContext.Authors.Attach(author);
        dbContext.Authors.Remove(author);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ChangeTracker.Clear();
        var service = new StorefrontQueryService(dbContext, new NoStock(), TimeProvider.System);

        var result = await service.ListBooksAsync(new BooksQuery { Q = $"tac gia da xoa {suffix}" }, CancellationToken);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListBooks_MatchesIsbnWithHyphens()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var (book, _) = await SaveBookAsync(dbContext);
        var isbn = await dbContext.Editions.Where(edition => edition.BookId == book.Id)
            .Select(edition => edition.Isbn)
            .SingleAsync(CancellationToken);
        var hyphenated = $"{isbn[..3]}-{isbn[3..6]}-{isbn[6..12]}-{isbn[12..]}";
        var storefront = new StorefrontQueryService(dbContext, new NoStock(), TimeProvider.System);
        var admin = new CatalogAdminService(dbContext, new NoStock(), TimeProvider.System);

        var storefrontResult = await storefront.ListBooksAsync(new BooksQuery { Q = hyphenated }, CancellationToken);
        var adminResult = await admin.ListBooksAsync(new AdminBooksQuery { Q = hyphenated }, CancellationToken);

        storefrontResult.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
        adminResult.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
    }

    [Fact]
    public async Task AdminListBooks_MatchesTitleIgnoringDiacritics()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var suffix = NewSuffix();
        var (book, _) = await SaveBookAsync(dbContext, title: $"Luật Thuế thu nhập {suffix}");
        var service = new CatalogAdminService(dbContext, new NoStock(), TimeProvider.System);

        var result = await service.ListBooksAsync(
            new AdminBooksQuery { Q = $"luat thue thu nhap {suffix}" }, CancellationToken);

        result.Items.Should().ContainSingle().Which.Id.Should().Be(book.Id);
    }

    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..10];

    private static async Task<(Book Book, string SkuCode)> SaveBookAsync(
        CatalogDbContext dbContext,
        string title = "Bộ luật Hình sự",
        Author? author = null)
    {
        var book = new Book(title, $"bo-luat-hinh-su-{Guid.NewGuid():N}", BookType.Code);
        if (author is not null)
        {
            book.AssignAuthors([author]);
        }

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
