using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Catalog.Application.Admin;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.IntegrationTests.Catalog;

public sealed class QuickCreateBookTests(CatalogDatabaseFixture fixture) : IClassFixture<CatalogDatabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task QuickCreate_SavesBookEditionAndSkusTogether()
    {
        await using var scope = fixture.CreateScope();
        var suffix = Unique();

        var created = await Service(scope).QuickCreateBookAsync(
            Request(suffix, Sku($"QC-{suffix}-BM", CoverType.Paperback), Sku($"QC-{suffix}-BC", CoverType.Hardcover)),
            CancellationToken);

        created.Slug.Should().Be($"quick-{suffix}");
        var edition = created.Editions.Should().ContainSingle().Subject;
        edition.IsLatest.Should().BeTrue("ấn bản đầu tiên tự là ấn bản mới nhất");
        // Domain đổi mã SKU sang chữ hoa.
        edition.Skus.Select(sku => sku.SkuCode).Should()
            .BeEquivalentTo($"QC-{suffix}-BM".ToUpperInvariant(), $"QC-{suffix}-BC".ToUpperInvariant());
    }

    [Fact]
    public async Task QuickCreate_DuplicateSkuCode_CreatesNothing()
    {
        await using var scope = fixture.CreateScope();
        var suffix = Unique();
        await Service(scope).QuickCreateBookAsync(
            Request(suffix, Sku($"QC-{suffix}-BM", CoverType.Paperback)), CancellationToken);

        var other = Unique();
        var act = () => Service(scope).QuickCreateBookAsync(
            Request(other, Sku($"QC-{suffix}-BM", CoverType.Paperback)), CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(CatalogErrors.SkuCodeDuplicateCode);
        await using var check = fixture.CreateScope();
        (await check.ServiceProvider.GetRequiredService<CatalogDbContext>().Books
            .AnyAsync(book => book.Slug == $"quick-{other}", CancellationToken)).Should().BeFalse(
                "đầu sách và ấn bản không được lưu khi SKU lỗi");
    }

    private static CatalogAdminService Service(IServiceScope scope) => new(
        scope.ServiceProvider.GetRequiredService<CatalogDbContext>(), new NoStock(), TimeProvider.System);

    private static string Unique() => Guid.NewGuid().ToString("N")[..10];

    private static QuickCreateBookRequest Request(string suffix, params CreateSkuRequest[] skus) => new(
        new UpsertBookRequest(
            $"Sách nhanh {suffix}", $"quick-{suffix}", BookType.Code, BookStatus.Active,
            null, null, null, null, null, null),
        new UpsertEditionRequest(
            Isbn(suffix), "NXB Tài Chính", "123/QĐ-NXBTC", EditionStatus.OnSale, null, null, 2026,
            null, null, 400, null, 800, null, null, null, null, null),
        skus);

    // ISBN-13 hợp lệ từ 9 chữ số ngẫu nhiên theo suffix: 978 + 9 số + số kiểm tra.
    private static string Isbn(string suffix)
    {
        var digits = "978" + string.Concat(suffix.Where(char.IsAsciiDigit).Append('1').Concat("000000000").Take(9));
        var sum = digits.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return digits + ((10 - (sum % 10)) % 10);
    }

    private static CreateSkuRequest Sku(string code, CoverType coverType) => new(code, coverType, 250_000m, 199_000m, false);

    private sealed class NoStock : IInventoryService
    {
        public Task<IReadOnlyDictionary<long, int>> GetAvailableQuantitiesAsync(
            IReadOnlyCollection<long> skuIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<long, int>>(new Dictionary<long, int>());

        public Task ReserveAsync(string reference, IReadOnlyCollection<StockLine> lines, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseAsync(string reference, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<long, decimal>> DeductAsync(string reference, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RestockAsync(string reference, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
