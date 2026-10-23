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
    public void Receipt_ApprovedByCreator_Throws()
    {
        var receipt = CreateReceipt(createdBy: 1);

        var act = () => receipt.Approve(approverId: 1, Now);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(InventoryErrors.ReceiptSelfApprovalCode);
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
