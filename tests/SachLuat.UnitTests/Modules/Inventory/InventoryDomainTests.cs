using AwesomeAssertions;
using SachLuat.Modules.Inventory.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Inventory;

public sealed class InventoryDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Receipt_ApprovedByAnotherUser_IsApproved()
    {
        var receipt = CreateReceipt(createdBy: 1);

        receipt.Approve(approverId: 2, Now);

        receipt.Status.Should().Be(ReceiptStatus.Approved);
        receipt.DecidedBy.Should().Be(2);
    }

    [Fact]
    public void Receipt_ApprovedByCreator_IsApproved()
    {
        var receipt = CreateReceipt(createdBy: 1);

        receipt.Approve(approverId: 1, Now);

        receipt.Status.Should().Be(ReceiptStatus.Approved);
        receipt.DecidedBy.Should().Be(1);
    }

    [Fact]
    public void Receipt_ReplaceDraft_SwapsNoteAndLines()
    {
        var receipt = CreateReceipt(createdBy: 1);

        receipt.Replace("Sửa số lượng", [new GoodsReceiptLine(11, "lo-02", null, 80, 30_000m, new DateOnly(2026, 10, 21))]);

        receipt.Note.Should().Be("Sửa số lượng");
        receipt.Lines.Should().ContainSingle().Which.Should().Match<GoodsReceiptLine>(
            line => line.SkuId == 11 && line.BatchCode == "LO-02" && line.Quantity == 80);
    }

    [Fact]
    public void Receipt_ReplaceWithoutLines_Throws()
    {
        var receipt = CreateReceipt(createdBy: 1);

        var act = () => receipt.Replace(null, []);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(InventoryErrors.ReceiptEmptyCode);
        receipt.Lines.Should().HaveCount(1, "phiếu giữ nguyên khi dữ liệu mới không hợp lệ");
    }

    [Fact]
    public void Receipt_ReplaceOrDeleteAfterDecision_Throws()
    {
        var receipt = CreateReceipt(createdBy: 1);
        receipt.Approve(approverId: 2, Now);

        var replace = () => receipt.Replace(null, [new GoodsReceiptLine(10, "LO-9", null, 1, 1m, new DateOnly(2026, 10, 21))]);
        var delete = receipt.EnsureCanDelete;

        replace.Should().Throw<BusinessException>().Which.Code.Should().Be(InventoryErrors.ReceiptNotDraftCode);
        delete.Should().Throw<BusinessException>().Which.Code.Should().Be(InventoryErrors.ReceiptNotDraftCode);
    }

    [Fact]
    public void Receipt_DecidedTwice_Throws()
    {
        var receipt = CreateReceipt(createdBy: 1);
        receipt.Reject(approverId: 2, "Sai số lượng", Now);

        var act = () => receipt.Approve(approverId: 2, Now);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(InventoryErrors.ReceiptNotDraftCode);
    }

    [Fact]
    public void Receipt_DuplicateBatchForSameSku_Throws()
    {
        var act = () => new GoodsReceipt(1, null,
        [
            new GoodsReceiptLine(10, "lo-01", null, 100, 50_000m, new DateOnly(2026, 10, 1)),
            new GoodsReceiptLine(10, "LO-01", null, 50, 50_000m, new DateOnly(2026, 10, 2)),
        ]);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(InventoryErrors.BatchCodeDuplicateCode);
    }

    [Fact]
    public void Receipt_WithoutLines_Throws()
    {
        var act = () => new GoodsReceipt(1, null, []);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(InventoryErrors.ReceiptEmptyCode);
    }

    [Fact]
    public void PrintBatch_TakeMoreThanRemaining_TakesOnlyRemaining()
    {
        var batch = new PrintBatch(10, "LO-01", null, quantity: 5, unitCost: 50_000m, new DateOnly(2026, 10, 1));

        batch.Take(3).Should().Be(3);
        batch.Take(3).Should().Be(2);
        batch.Remaining.Should().Be(0);
    }

    [Fact]
    public void PrintBatch_PutBack_NeverExceedsQuantity()
    {
        var batch = new PrintBatch(10, "LO-01", null, quantity: 5, unitCost: 50_000m, new DateOnly(2026, 10, 1));
        batch.Take(2);

        batch.PutBack(10);

        batch.Remaining.Should().Be(5);
    }

    private static GoodsReceipt CreateReceipt(long createdBy) => new(createdBy, "Nhập lô tháng 10",
    [
        new GoodsReceiptLine(10, "LO-2026-10", "Xưởng in A", 500, 45_000m, new DateOnly(2026, 10, 20)),
    ]);
}
