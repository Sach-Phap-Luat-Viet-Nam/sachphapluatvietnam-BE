using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Content.Application;
using SachLuat.Modules.Content.Domain;
using SachLuat.Modules.Content.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.IntegrationTests.Content;

/// <summary>
/// Mỗi test dùng slug riêng có tiền tố <see cref="TestSlugPrefix"/> và tự dọn, để không đụng hai trang chính sách
/// được migration seed trong database test dùng chung.
/// </summary>
public sealed class StaticPagePersistenceTests(ContentDatabaseFixture fixture)
    : IClassFixture<ContentDatabaseFixture>, IAsyncLifetime
{
    private const string TestSlugPrefix = "test-page-";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_SeedsPublishedPolicyPages()
    {
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StaticPageService>();

        var terms = await service.GetPublishedAsync("dieu-khoan-su-dung", CancellationToken);
        var privacy = await service.GetPublishedAsync("chinh-sach-bao-mat", CancellationToken);

        terms.Title.Should().Be("Điều khoản sử dụng");
        terms.ContentHtml.Should().Contain("<h2 id=\"pham-vi\">");
        privacy.Title.Should().Be("Chính sách bảo mật");
    }

    [Fact]
    public async Task GetPublished_IgnoresCaseOfSlug()
    {
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StaticPageService>();
        var slug = NewSlug();
        await service.CreateAsync(Request(slug), CancellationToken);

        var page = await service.GetPublishedAsync(slug.ToUpperInvariant(), CancellationToken);

        page.Slug.Should().Be(slug);
        page.ContentHtml.Should().Be("<p>Nội dung</p>");
    }

    [Fact]
    public async Task GetPublished_Unpublished_IsNotFound()
    {
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StaticPageService>();
        var slug = NewSlug();
        await service.CreateAsync(Request(slug) with { IsPublished = false }, CancellationToken);

        var act = () => service.GetPublishedAsync(slug, CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Create_DuplicateSlug_IsConflict()
    {
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StaticPageService>();
        var slug = NewSlug();
        await service.CreateAsync(Request(slug), CancellationToken);

        var act = () => service.CreateAsync(Request(slug) with { Title = "Trùng" }, CancellationToken);

        var error = (await act.Should().ThrowAsync<BusinessException>()).Which;
        error.Code.Should().Be(ContentErrors.SlugDuplicateCode);
        error.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Update_ChangesPublicContent()
    {
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StaticPageService>();
        var slug = NewSlug();
        var created = await service.CreateAsync(Request(slug), CancellationToken);

        await service.UpdateAsync(
            created.Id,
            Request(slug) with { Title = "Đã sửa", ContentHtml = "<p>Bản mới</p>" },
            CancellationToken);
        var page = await service.GetPublishedAsync(slug, CancellationToken);

        page.Title.Should().Be("Đã sửa");
        page.ContentHtml.Should().Be("<p>Bản mới</p>");
    }

    [Fact]
    public async Task Delete_RemovesPage()
    {
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StaticPageService>();
        var created = await service.CreateAsync(Request(NewSlug()), CancellationToken);

        await service.DeleteAsync(created.Id, CancellationToken);

        var act = () => service.GetAsync(created.Id, CancellationToken);
        (await act.Should().ThrowAsync<BusinessException>()).Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task List_OrdersBySlug_WithoutContent()
    {
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StaticPageService>();
        await service.CreateAsync(Request(TestSlugPrefix + "b"), CancellationToken);
        await service.CreateAsync(Request(TestSlugPrefix + "a"), CancellationToken);

        var list = await service.ListAsync(new PagedQuery { Limit = 100 }, CancellationToken);

        var slugs = list.Items.Select(item => item.Slug).ToList();
        slugs.Should().BeInAscendingOrder(StringComparer.Ordinal);
        slugs.Should().Contain([TestSlugPrefix + "a", TestSlugPrefix + "b"]);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (fixture.ConnectionString is null)
        {
            return;
        }

        await using var scope = fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ContentDbContext>().StaticPages
            .Where(page => page.Slug.StartsWith(TestSlugPrefix))
            .ExecuteDeleteAsync();
    }

    private static string NewSlug() => TestSlugPrefix + Guid.NewGuid().ToString("N")[..12];

    private static UpsertStaticPageRequest Request(string slug) =>
        new(slug, "Trang thử", "Mô tả", "<p>Nội dung</p>", IsPublished: true);
}
