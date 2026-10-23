using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.Modules.Inventory.Domain;
using SachLuat.Modules.Inventory.Infrastructure.Persistence;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Inventory.Application;

internal sealed class InventoryService(
    InventoryDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<InventoryService> logger) : IInventoryService
{
    public async Task<IReadOnlyDictionary<long, int>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<long> skuIds,
        CancellationToken cancellationToken)
    {
        if (skuIds.Count == 0)
        {
            return new Dictionary<long, int>();
        }

        var ids = skuIds.Distinct().ToList();
        return await dbContext.StockItems.AsNoTracking()
            .Where(stock => ids.Contains(stock.SkuId))
            .ToDictionaryAsync(stock => stock.SkuId, stock => stock.Available, cancellationToken);
    }

    public async Task ReserveAsync(
        string reference,
        IReadOnlyCollection<StockLine> lines,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Reservations.AnyAsync(r => r.Reference == reference, cancellationToken))
        {
            return;
        }

        var now = Now();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Cập nhật theo thứ tự sku_id để hai đơn cùng lúc không khóa chéo nhau (deadlock).
        foreach (var line in Merge(lines))
        {
            var affected = await dbContext.StockItems
                .Where(stock => stock.SkuId == line.SkuId && stock.Available >= line.Quantity)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(stock => stock.Available, stock => stock.Available - line.Quantity)
                        .SetProperty(stock => stock.Reserved, stock => stock.Reserved + line.Quantity)
                        .SetProperty(stock => stock.UpdatedAt, now),
                    cancellationToken);
            if (affected == 0)
            {
                throw InventoryErrors.OutOfStock();
            }

            dbContext.Reservations.Add(new StockReservation(reference, line.SkuId, line.Quantity));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReleaseAsync(string reference, CancellationToken cancellationToken)
    {
        var now = Now();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var reservations = await ActiveReservationsAsync(reference, ReservationStatus.Active, cancellationToken);

        foreach (var reservation in reservations)
        {
            await dbContext.StockItems
                .Where(stock => stock.SkuId == reservation.SkuId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(stock => stock.Available, stock => stock.Available + reservation.Quantity)
                        .SetProperty(stock => stock.Reserved, stock => stock.Reserved - reservation.Quantity)
                        .SetProperty(stock => stock.UpdatedAt, now),
                    cancellationToken);
            reservation.MarkReleased();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> DeductAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        var now = Now();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var reservations = await ActiveReservationsAsync(reference, ReservationStatus.Active, cancellationToken);
        var unitCosts = new Dictionary<long, decimal>();

        foreach (var reservation in reservations)
        {
            var totalCost = await TakeFifoAsync(
                reservation.SkuId, reservation.Quantity, TransactionType.Sale, reference, note: null, cancellationToken);
            await dbContext.StockItems
                .Where(stock => stock.SkuId == reservation.SkuId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(stock => stock.Reserved, stock => stock.Reserved - reservation.Quantity)
                        .SetProperty(stock => stock.UpdatedAt, now),
                    cancellationToken);
            reservation.MarkDeducted();
            unitCosts[reservation.SkuId] = decimal.Round(totalCost / reservation.Quantity, 0);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return unitCosts;
    }

    public async Task RestockAsync(string reference, CancellationToken cancellationToken)
    {
        var now = Now();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var reservations = await ActiveReservationsAsync(reference, ReservationStatus.Deducted, cancellationToken);

        foreach (var reservation in reservations)
        {
            var sales = await dbContext.Transactions
                .Where(t => t.Reference == reference && t.SkuId == reservation.SkuId && t.Type == TransactionType.Sale)
                .ToListAsync(cancellationToken);
            var batchIds = sales.Select(sale => sale.PrintBatchId).OfType<long>().ToList();
            var batches = await dbContext.PrintBatches
                .Where(batch => batchIds.Contains(batch.Id))
                .ToDictionaryAsync(batch => batch.Id, cancellationToken);

            // Trả về đúng lô đã xuất để giá vốn và FIFO không lệch.
            foreach (var sale in sales.Where(sale => sale.PrintBatchId is not null))
            {
                var quantity = -sale.Quantity;
                batches[sale.PrintBatchId!.Value].PutBack(quantity);
                dbContext.Transactions.Add(new InventoryTransaction(
                    reservation.SkuId, sale.PrintBatchId, TransactionType.Return, quantity, reference,
                    currentUser.UserId));
            }

            await dbContext.StockItems
                .Where(stock => stock.SkuId == reservation.SkuId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(stock => stock.Available, stock => stock.Available + reservation.Quantity)
                        .SetProperty(stock => stock.UpdatedAt, now),
                    cancellationToken);
            reservation.MarkReturned();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Xuất <paramref name="quantity"/> cuốn theo FIFO lô in (BR-13), khóa dòng các lô để không xuất trùng.
    /// Phải gọi trong transaction. Trả tổng giá vốn.
    /// </summary>
    internal async Task<decimal> TakeFifoAsync(
        long skuId,
        int quantity,
        TransactionType type,
        string reference,
        string? note,
        CancellationToken cancellationToken)
    {
        var batches = (await dbContext.PrintBatches
                .FromSql($"""
                    SELECT * FROM inventory.print_batches
                    WHERE sku_id = {skuId} AND remaining > 0
                    FOR UPDATE
                    """)
                .ToListAsync(cancellationToken))
            .OrderBy(batch => batch.ReceivedAt)
            .ThenBy(batch => batch.Id);

        var remaining = quantity;
        var totalCost = 0m;
        foreach (var batch in batches)
        {
            var taken = batch.Take(remaining);
            if (taken == 0)
            {
                continue;
            }

            totalCost += taken * batch.UnitCost;
            remaining -= taken;
            dbContext.Transactions.Add(new InventoryTransaction(
                skuId, batch.Id, type, -taken, reference, currentUser.UserId, note));
            if (remaining == 0)
            {
                break;
            }
        }

        if (remaining > 0)
        {
            // Chỉ xảy ra khi tồn lệch với lô in (vi phạm bất biến): dừng lại thay vì xuất âm.
            logger.LogError(
                "Stock of SKU {SkuId} is inconsistent with print batches: missing {Missing} for {Reference}",
                skuId, remaining, reference);
            throw new InvalidOperationException($"Inventory of SKU {skuId} is inconsistent with print batches.");
        }

        return totalCost;
    }

    private Task<List<StockReservation>> ActiveReservationsAsync(
        string reference,
        ReservationStatus status,
        CancellationToken cancellationToken) =>
        dbContext.Reservations
            .Where(r => r.Reference == reference && r.Status == status)
            .OrderBy(r => r.SkuId)
            .ToListAsync(cancellationToken);

    private static IEnumerable<StockLine> Merge(IEnumerable<StockLine> lines) => lines
        .GroupBy(line => line.SkuId)
        .Select(group => new StockLine(group.Key, group.Sum(line => line.Quantity)))
        .OrderBy(line => line.SkuId);

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
