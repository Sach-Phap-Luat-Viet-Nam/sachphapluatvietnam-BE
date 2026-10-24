using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Content.Application;
using SachLuat.Modules.Content.Domain;
using SachLuat.Modules.Content.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using Xunit;

namespace SachLuat.IntegrationTests.Content;

public sealed class PopupPersistenceTests(ContentDatabaseFixture fixture) : IClassFixture<ContentDatabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private DateTimeOffset Now => fixture.Clock.GetUtcNow();

    [Fact]
    public async Task GetActive_PicksHighestPriorityAmongRunningPopups()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<PopupService>();

        await service.CreateAsync(Request("Đang chạy, ưu tiên thấp", priority: 5), CancellationToken);
        var winner = await service.CreateAsync(Request("Đang chạy, ưu tiên cao", priority: 10), CancellationToken);
        await service.CreateAsync(Request("Đã tắt", priority: 100) with { IsActive = false }, CancellationToken);
        await service.CreateAsync(
            Request("Chưa tới lịch", priority: 100) with { StartsAt = Now.AddDays(1), EndsAt = null },
            CancellationToken);
        await service.CreateAsync(
            Request("Đã hết lịch", priority: 100) with { StartsAt = Now.AddDays(-7), EndsAt = Now.AddDays(-1) },
            CancellationToken);
        var deleted = await service.CreateAsync(Request("Đã xóa", priority: 100), CancellationToken);
        await service.DeleteAsync(deleted.Id, CancellationToken);

        var active = await service.GetActiveAsync(CancellationToken);

        active.Should().NotBeNull();
        active!.Id.Should().Be(winner.Id);
        active.DisplayFrequency.Should().Be(PopupDisplayFrequency.OncePerDay);
    }

    [Fact]
    public async Task GetActive_NothingRunning_ReturnsNull()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<PopupService>();
        await service.CreateAsync(Request("Đã tắt", priority: 1) with { IsActive = false }, CancellationToken);

        (await service.GetActiveAsync(CancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task GetRunning_ReturnsAtMostThreeRunningPopupsByPriority()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<PopupService>();

        var low = await service.CreateAsync(Request("Ưu tiên thấp", priority: 1), CancellationToken);
        var third = await service.CreateAsync(Request("Ưu tiên 5", priority: 5), CancellationToken);
        var first = await service.CreateAsync(Request("Ưu tiên 30", priority: 30), CancellationToken);
        var second = await service.CreateAsync(Request("Ưu tiên 20", priority: 20), CancellationToken);
        await service.CreateAsync(Request("Đã tắt", priority: 100) with { IsActive = false }, CancellationToken);
        await service.CreateAsync(
            Request("Đã hết lịch", priority: 100) with { StartsAt = Now.AddDays(-7), EndsAt = Now.AddDays(-1) },
            CancellationToken);

        var running = await service.GetRunningAsync(CancellationToken);

        running.Select(popup => popup.Id).Should().Equal(first.Id, second.Id, third.Id);
        running.Select(popup => popup.Id).Should().NotContain(low.Id);
        // Phần tử đầu cũng là popup tự bật lúc vào web.
        (await service.GetActiveAsync(CancellationToken))!.Id.Should().Be(running[0].Id);
    }

    [Fact]
    public async Task GetRunning_NothingRunning_ReturnsEmpty()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<PopupService>();
        await service.CreateAsync(Request("Đã tắt", priority: 1) with { IsActive = false }, CancellationToken);

        (await service.GetRunningAsync(CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task CreateUpdateDelete_StoresSnakeCaseAndSoftDeletes()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<PopupService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ContentDbContext>();

        var created = await service.CreateAsync(
            Request("Ra mắt sách mới", priority: 1) with { StartsAt = Now.AddDays(2) }, CancellationToken);
        created.Status.Should().Be(PopupStatus.Scheduled);
        var frequency = await dbContext.Database
            .SqlQuery<string>($"SELECT display_frequency AS \"Value\" FROM content.popups WHERE id = {created.Id}")
            .SingleAsync(CancellationToken);
        frequency.Should().Be("once_per_day");

        var updated = await service.UpdateAsync(
            created.Id, Request("Ra mắt sách mới", priority: 1), CancellationToken);
        updated.Status.Should().Be(PopupStatus.Running);
        (await service.ListAsync(new PagedQuery(), CancellationToken)).Items.Should().ContainSingle();

        await service.DeleteAsync(created.Id, CancellationToken);
        (await service.ListAsync(new PagedQuery(), CancellationToken)).Meta.Total.Should().Be(0);
        var deletedAt = await dbContext.Popups.IgnoreQueryFilters().AsNoTracking()
            .Where(popup => popup.Id == created.Id)
            .Select(popup => popup.DeletedAt)
            .SingleAsync(CancellationToken);
        deletedAt.Should().NotBeNull();
    }

    private UpsertPopupRequest Request(string title, int priority) => new(
        title,
        "https://res.cloudinary.com/demo-cloud/image/upload/sachluat/popups/tet.jpg",
        "/khuyen-mai",
        Now.AddDays(-1),
        Now.AddDays(6),
        IsActive: true,
        priority,
        PopupDisplayFrequency.OncePerDay);
}
