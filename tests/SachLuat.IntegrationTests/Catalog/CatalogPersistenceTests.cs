using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Categories;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace SachLuat.IntegrationTests.Catalog;

public sealed class CatalogPersistenceTests(CatalogDatabaseFixture fixture) : IClassFixture<CatalogDatabaseFixture>
{
    private const string UniqueViolation = "23505";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveBook_SetsTimestampsAndStoresEnumsAsSnakeCase()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var book = NewBook();
        var edition = book.AddEdition(NewPublishingInfo());
        edition.ChangeStatus(EditionStatus.OnSale);
        edition.AddSku(NewSkuCode(), CoverType.Paperback, 250_000m, 199_000m);
        book.SetLatestEdition(edition);
        book.UpdateDetails(BookType.Code, null, [TargetAudience.Lawyer, TargetAudience.Student], null, null);

        dbContext.Books.Add(book);
        await dbContext.SaveChangesAsync(CancellationToken);

        book.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        book.UpdatedAt.Should().Be(book.CreatedAt);
        var status = await dbContext.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM catalog.editions WHERE id = {edition.Id}")
            .SingleAsync(CancellationToken);
        status.Should().Be("on_sale");

        dbContext.ChangeTracker.Clear();
        var loaded = await dbContext.Books
            .Include(b => b.Editions).ThenInclude(e => e.Skus)
            .SingleAsync(b => b.Id == book.Id, CancellationToken);
        loaded.TargetAudiences.Should().Equal(TargetAudience.Student, TargetAudience.Lawyer);
        loaded.LatestEdition!.Skus.Single().SalePrice.Should().Be(199_000m);
    }

    [Fact]
    public async Task TwoLatestEditionsForSameBook_RejectedByDatabase()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var book = NewBook();
        book.AddEdition(NewPublishingInfo());
        book.AddEdition(NewPublishingInfo());
        dbContext.Books.Add(book);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Bỏ qua domain để kiểm tra lớp bảo vệ cuối cùng của BR-02 trong CSDL.
        var act = () => dbContext.Database.ExecuteSqlAsync(
            $"UPDATE catalog.editions SET is_latest = true WHERE book_id = {book.Id}",
            CancellationToken);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(UniqueViolation);
    }

    [Fact]
    public async Task DuplicateIsbnAcrossBooks_RejectedByDatabase()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var publishingInfo = NewPublishingInfo();
        var first = NewBook();
        first.AddEdition(publishingInfo);
        var second = NewBook();
        second.AddEdition(publishingInfo);
        dbContext.Books.AddRange(first, second);

        var act = () => dbContext.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>().Which.SqlState.Should().Be(UniqueViolation);
    }

    [Fact]
    public async Task RemoveCategory_SoftDeletesAndHidesFromQueries()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = GetDbContext(scope);
        var category = new Category("Dân sự", $"dan-su-{Guid.NewGuid():N}");
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(CancellationToken);

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ChangeTracker.Clear();

        (await dbContext.Categories.AnyAsync(c => c.Id == category.Id, CancellationToken)).Should().BeFalse();
        var deleted = await dbContext.Categories.IgnoreQueryFilters()
            .SingleAsync(c => c.Id == category.Id, CancellationToken);
        deleted.DeletedAt.Should().NotBeNull();
    }

    private static CatalogDbContext GetDbContext(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

    private static Book NewBook() => new("Bộ luật Dân sự", $"bo-luat-dan-su-{Guid.NewGuid():N}", BookType.Code);

    private static EditionPublishingInfo NewPublishingInfo() =>
        new(NewIsbn(), "NXB Tư pháp", "123/QĐ-NXBTP");

    private static string NewSkuCode() => $"SLVN-{Guid.NewGuid():N}"[..30].ToUpperInvariant();

    private static string NewIsbn()
    {
        var digits = "978" + Random.Shared.NextInt64(0, 1_000_000_000).ToString("D9");
        return digits + Isbn13.ComputeCheckDigit(digits);
    }
}
