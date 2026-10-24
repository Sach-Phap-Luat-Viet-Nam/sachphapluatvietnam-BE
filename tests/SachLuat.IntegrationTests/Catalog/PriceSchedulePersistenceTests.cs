using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Catalog.Application;
using SachLuat.Modules.Catalog.Application.Storefront;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.Modules.Inventory.Contracts;
using Xunit;

namespace SachLuat.IntegrationTests.Catalog;

/// <summary>Giá hiện hành tính trong SQL (lọc, sắp xếp, đọc SKU) phải khớp <c>Sku.CurrentPriceAt</c>.</summary>
public sealed class PriceSchedulePersistenceTests(CatalogDatabaseFixture fixture)
    : IClassFixture<CatalogDatabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SkuReader_ReturnsSchedulePriceOnlyWhileRunning()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var now = DateTime.UtcNow;
        var (_, running) = await SaveBookAsync(dbContext, "running", 199_000m, sku =>
            sku.AddPriceSchedule(150_000m, now.AddHours(-1), now.AddHours(1), null, now));
        var (_, upcoming) = await SaveBookAsync(dbContext, "upcoming", 199_000m, sku =>
            sku.AddPriceSchedule(150_000m, now.AddHours(1), now.AddHours(2), null, now));

        var skus = await new CatalogSkuReader(dbContext, TimeProvider.System)
            .GetSkusAsync([running.Id, upcoming.Id], CancellationToken);

        skus[running.Id].SalePrice.Should().Be(150_000m);
        skus[running.Id].ListPrice.Should().Be(250_000m);
        skus[upcoming.Id].SalePrice.Should().Be(199_000m);
    }

    [Fact]
    public async Task ListBooks_FiltersAndSortsByCurrentPrice()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var now = DateTime.UtcNow;
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (discounted, _) = await SaveBookAsync(dbContext, tag, 250_000m, sku =>
            sku.AddPriceSchedule(120_000m, now.AddHours(-1), now.AddHours(1), "Sale", now));
        var (regular, _) = await SaveBookAsync(dbContext, tag, 250_000m, _ => { });
        dbContext.ChangeTracker.Clear();
        var service = new StorefrontQueryService(dbContext, new NoStock(), TimeProvider.System);

        var sorted = await service.ListBooksAsync(
            new BooksQuery { Q = tag, Sort = BookSorts.PriceAsc }, CancellationToken);
        var cheap = await service.ListBooksAsync(
            new BooksQuery { Q = tag, MaxPrice = 150_000m }, CancellationToken);
        var onSale = await service.ListBooksAsync(new BooksQuery { Q = tag, OnSale = true }, CancellationToken);

        sorted.Items.Select(book => book.Id).Should().Equal(discounted.Id, regular.Id);
        sorted.Items[0].SalePrice.Should().Be(120_000m);
        sorted.Items[0].DiscountPercent.Should().Be(52);
        sorted.Items[0].SaleEndsAt.Should().NotBeNull();
        sorted.Items[1].SaleEndsAt.Should().BeNull();
        cheap.Items.Should().ContainSingle(book => book.Id == discounted.Id);
        onSale.Items.Should().ContainSingle(book => book.Id == discounted.Id);
    }

    [Fact]
    public async Task GetBook_ReturnsCurrentAndRegularPrice()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var now = DateTime.UtcNow;
        var (book, _) = await SaveBookAsync(dbContext, "detail", 199_000m, sku =>
            sku.AddPriceSchedule(150_000m, now.AddHours(-1), now.AddHours(1), null, now));
        dbContext.ChangeTracker.Clear();
        var service = new StorefrontQueryService(dbContext, new NoStock(), TimeProvider.System);

        var detail = await service.GetBookAsync(book.Slug, CancellationToken);

        var sku = detail.Editions.Single().Skus.Single();
        sku.SalePrice.Should().Be(150_000m);
        sku.RegularSalePrice.Should().Be(199_000m);
        sku.DiscountPercent.Should().Be(40);
        sku.SaleEndsAt.Should().BeCloseTo(now.AddHours(1), TimeSpan.FromMilliseconds(1));
    }

    private static async Task<(Book Book, Sku Sku)> SaveBookAsync(
        CatalogDbContext dbContext,
        string tag,
        decimal salePrice,
        Action<Sku> configure)
    {
        var book = new Book($"Sách giá {tag}", $"sach-gia-{Guid.NewGuid():N}", BookType.Code);
        book.ChangeStatus(BookStatus.Active);
        var edition = book.AddEdition(new EditionPublishingInfo(NewIsbn(), "NXB Tư pháp", "123/QĐ-NXBTP"));
        edition.ChangeStatus(EditionStatus.OnSale);
        book.SetLatestEdition(edition);
        var sku = edition.AddSku(
            $"SLVN-{Guid.NewGuid():N}"[..30].ToUpperInvariant(), CoverType.Paperback, 250_000m, salePrice);
        configure(sku);

        dbContext.Books.Add(book);
        await dbContext.SaveChangesAsync(CancellationToken);
        return (book, sku);
    }

    private static CatalogDbContext GetDbContext(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

    private static string NewIsbn()
    {
        var digits = "978" + Random.Shared.NextInt64(0, 1_000_000_000).ToString("D9");
        return digits + Isbn13.ComputeCheckDigit(digits);
    }

    private sealed class NoStock : IInventoryService
    {
        public Task<IReadOnlyDictionary<long, int>> GetAvailableQuantitiesAsync(
            IReadOnlyCollection<long> skuIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<long, int>>(new Dictionary<long, int>());

        public Task ReserveAsync(
            string reference,
            IReadOnlyCollection<StockLine> lines,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReleaseAsync(string reference, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<long, decimal>> DeductAsync(
            string reference,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RestockAsync(string reference, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
