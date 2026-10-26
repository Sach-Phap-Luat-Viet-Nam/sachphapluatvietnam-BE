using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using SachLuat.Modules.Inventory.Application;
using SachLuat.Modules.Inventory.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.IntegrationTests.Inventory;

[Collection(InventoryDatabaseCollection.Name)]
public sealed class ReceiptPersistenceTests(InventoryDatabaseFixture fixture)
{
    private const long Keeper = 10;
    private const long OtherKeeper = 11;
    private const long SuperAdmin = 1;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_ShowsBookTitlesAndCreatorName()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();

        var receipt = await Service(scope).CreateReceiptAsync(Keeper, Request(Line(1, "lo-01")), CancellationToken);

        receipt.CreatedByName.Should().Be("Nhân viên 10");
        receipt.Lines.Should().ContainSingle().Which.Should().Match<ReceiptLineResponse>(
            line => line.BookTitle == "Bộ luật Dân sự 2026" && line.CoverType == "paperback" && line.BatchCode == "LO-01");
    }

    [Fact]
    public async Task Update_ReplacesLinesAndNoteOfADraft()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var created = await Service(scope).CreateReceiptAsync(
            Keeper, Request(Line(1, "lo-01"), Line(2, "lo-02")), CancellationToken);

        var updated = await Service(scope).UpdateReceiptAsync(
            Keeper, isSuperAdmin: false, created.Id, Request(Line(2, "lo-03", quantity: 40)) with { Note = "Đã sửa" },
            CancellationToken);

        updated.Note.Should().Be("Đã sửa");
        updated.Lines.Should().ContainSingle().Which.Should().Match<ReceiptLineResponse>(
            line => line.SkuId == 2 && line.BatchCode == "LO-03" && line.Quantity == 40);

        await using var other = fixture.CreateScopeForTests();
        (await Service(other).GetReceiptAsync(created.Id, CancellationToken)).Lines.Should().HaveCount(1,
            "các dòng cũ phải bị xóa khỏi CSDL, không chỉ khỏi bộ nhớ");
    }

    [Fact]
    public async Task Update_ByAnotherKeeper_IsForbidden_ButSuperAdminMay()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var created = await Service(scope).CreateReceiptAsync(Keeper, Request(Line(1, "lo-01")), CancellationToken);

        var other = () => Service(scope).UpdateReceiptAsync(
            OtherKeeper, isSuperAdmin: false, created.Id, Request(Line(1, "lo-09")), CancellationToken);
        var admin = await Service(scope).UpdateReceiptAsync(
            SuperAdmin, isSuperAdmin: true, created.Id, Request(Line(1, "lo-09")), CancellationToken);

        (await other.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(InventoryErrors.ReceiptForbiddenCode);
        admin.Lines.Single().BatchCode.Should().Be("LO-09");
    }

    [Fact]
    public async Task Delete_RemovesADraftWithItsLines()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var created = await Service(scope).CreateReceiptAsync(Keeper, Request(Line(1, "lo-01")), CancellationToken);

        await Service(scope).DeleteReceiptAsync(Keeper, isSuperAdmin: false, created.Id, CancellationToken);

        var get = () => Service(scope).GetReceiptAsync(created.Id, CancellationToken);
        (await get.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task ApprovedReceipt_CannotBeEditedOrDeleted_AndSuperAdminCanApproveTheirOwn()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var created = await Service(scope).CreateReceiptAsync(SuperAdmin, Request(Line(1, "lo-01")), CancellationToken);

        var approved = await Service(scope).ApproveReceiptAsync(SuperAdmin, created.Id, CancellationToken);
        var update = () => Service(scope).UpdateReceiptAsync(
            SuperAdmin, true, created.Id, Request(Line(1, "lo-02")), CancellationToken);
        var delete = () => Service(scope).DeleteReceiptAsync(SuperAdmin, true, created.Id, CancellationToken);

        approved.Status.Should().Be(ReceiptStatus.Approved);
        approved.DecidedByName.Should().Be("Nhân viên 1");
        (await update.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(InventoryErrors.ReceiptNotDraftCode);
        (await delete.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(InventoryErrors.ReceiptNotDraftCode);
    }

    [Fact]
    public async Task Update_BatchCodeAlreadyReceived_IsRejected_AndKeepsTheOldLines()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var first = await Service(scope).CreateReceiptAsync(SuperAdmin, Request(Line(1, "lo-01")), CancellationToken);
        await Service(scope).ApproveReceiptAsync(SuperAdmin, first.Id, CancellationToken);
        var draft = await Service(scope).CreateReceiptAsync(Keeper, Request(Line(1, "lo-02")), CancellationToken);

        var act = () => Service(scope).UpdateReceiptAsync(
            Keeper, false, draft.Id, Request(Line(1, "lo-01")), CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(InventoryErrors.BatchCodeDuplicateCode);
    }

    [Fact]
    public async Task SearchSkus_MatchesTitleOrCode_WithAvailableStock()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var receipt = await Service(scope).CreateReceiptAsync(
            SuperAdmin, Request(Line(1, "lo-01", quantity: 120)), CancellationToken);
        await Service(scope).ApproveReceiptAsync(SuperAdmin, receipt.Id, CancellationToken);

        var byTitle = await Service(scope).SearchSkusAsync(new SkuSearchQuery { Q = "dân sự" }, CancellationToken);
        var byCode = await Service(scope).SearchSkusAsync(new SkuSearchQuery { Q = "PL-002" }, CancellationToken);

        byTitle.Should().ContainSingle().Which.Should().Match<SkuSearchResponse>(
            sku => sku.SkuId == 1 && sku.Available == 120 && sku.CoverType == "paperback");
        byCode.Should().ContainSingle().Which.Available.Should().Be(0);
    }

    [Fact]
    public async Task StockCard_ShowsBookAndWhoDidEachMovement()
    {
        await using var scope = await fixture.CreateCleanScopeAsync();
        var receipt = await Service(scope).CreateReceiptAsync(Keeper, Request(Line(1, "lo-01")), CancellationToken);
        await Service(scope).ApproveReceiptAsync(SuperAdmin, receipt.Id, CancellationToken);

        var card = await Service(scope).GetStockCardAsync(1, CancellationToken);

        card.BookTitle.Should().Be("Bộ luật Dân sự 2026");
        card.RecentTransactions.Should().ContainSingle().Which.CreatedByName.Should().Be("Nhân viên 1");
    }

    private static InventoryAdminService Service(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<InventoryAdminService>();

    private static ReceiptLineRequest Line(long skuId, string batch, int quantity = 100) =>
        new(skuId, batch, null, quantity, 45_000m, new DateOnly(2026, 10, 20));

    private static CreateReceiptRequest Request(params ReceiptLineRequest[] lines) => new(null, lines);
}
