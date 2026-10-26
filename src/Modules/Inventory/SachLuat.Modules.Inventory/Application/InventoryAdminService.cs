using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Inventory.Domain;
using SachLuat.Modules.Inventory.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Inventory.Application;

/// <summary>Nghiệp vụ kho trên Admin (FR-80, FR-81, BR-50).</summary>
internal sealed class InventoryAdminService(
    InventoryDbContext dbContext,
    InventoryService inventoryService,
    ICatalogSkuReader skuReader,
    IUserNameReader userNames,
    TimeProvider timeProvider,
    ILogger<InventoryAdminService> logger)
{
    private const int RecentTransactionLimit = 100;

    public async Task<ReceiptResponse> CreateReceiptAsync(
        long userId,
        CreateReceiptRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureSkusExistAsync(request, cancellationToken);
        var receipt = new GoodsReceipt(userId, request.Note, ToLines(request));
        await EnsureBatchCodesAvailableAsync(receipt, cancellationToken);

        dbContext.GoodsReceipts.Add(receipt);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} created goods receipt {ReceiptId}", userId, receipt.Id);
        return await GetReceiptAsync(receipt.Id, cancellationToken);
    }

    /// <summary>Sửa phiếu nháp (thay ghi chú và toàn bộ dòng); chỉ người lập hoặc Super Admin.</summary>
    public async Task<ReceiptResponse> UpdateReceiptAsync(
        long userId,
        bool isSuperAdmin,
        long receiptId,
        CreateReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var receipt = await dbContext.GoodsReceipts.Include(r => r.Lines)
            .SingleOrDefaultAsync(r => r.Id == receiptId, cancellationToken)
            ?? throw InventoryErrors.NotFound("phiếu nhập");
        EnsureCanModify(receipt, userId, isSuperAdmin);
        await EnsureSkusExistAsync(request, cancellationToken);

        receipt.Replace(request.Note, ToLines(request));
        await EnsureBatchCodesAvailableAsync(receipt, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} updated draft goods receipt {ReceiptId}", userId, receiptId);
        return await GetReceiptAsync(receiptId, cancellationToken);
    }

    /// <summary>Xóa phiếu nháp (chưa ảnh hưởng tồn kho); phiếu đã duyệt hoặc từ chối giữ lại làm chứng từ.</summary>
    public async Task DeleteReceiptAsync(
        long userId,
        bool isSuperAdmin,
        long receiptId,
        CancellationToken cancellationToken)
    {
        var receipt = await dbContext.GoodsReceipts.Include(r => r.Lines)
            .SingleOrDefaultAsync(r => r.Id == receiptId, cancellationToken)
            ?? throw InventoryErrors.NotFound("phiếu nhập");
        EnsureCanModify(receipt, userId, isSuperAdmin);
        receipt.EnsureCanDelete();

        dbContext.GoodsReceipts.Remove(receipt);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} deleted draft goods receipt {ReceiptId}", userId, receiptId);
    }

    /// <summary>Tìm SKU để chọn nhanh khi lập phiếu: một phần tên sách, mã SKU hoặc ISBN, kèm tồn khả dụng.</summary>
    public async Task<IReadOnlyList<SkuSearchResponse>> SearchSkusAsync(
        SkuSearchQuery query,
        CancellationToken cancellationToken)
    {
        var skus = await skuReader.SearchSkusAsync(query.Q, query.Limit, cancellationToken);
        var ids = skus.Select(sku => sku.SkuId).ToList();
        var available = await dbContext.StockItems.AsNoTracking()
            .Where(stock => ids.Contains(stock.SkuId))
            .ToDictionaryAsync(stock => stock.SkuId, stock => stock.Available, cancellationToken);
        return skus.Select(sku => new SkuSearchResponse(
            sku.SkuId,
            sku.SkuCode,
            sku.BookTitle,
            sku.EditionName,
            sku.CoverType,
            sku.Isbn,
            sku.IsLatestEdition,
            available.GetValueOrDefault(sku.SkuId))).ToList();
    }

    public async Task<PagedResponse<ReceiptResponse>> ListReceiptsAsync(
        ReceiptsQuery query,
        CancellationToken cancellationToken)
    {
        var receipts = dbContext.GoodsReceipts.AsNoTracking().Include(r => r.Lines).AsQueryable();
        if (SnakeCaseEnumConverter<ReceiptStatus>.TryParse(query.Status, out var status))
        {
            receipts = receipts.Where(receipt => receipt.Status == status);
        }

        var total = await receipts.CountAsync(cancellationToken);
        var page = await receipts
            .OrderByDescending(receipt => receipt.Id)
            .Skip(query.Skip)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
        var skus = await skuReader.GetSkusAsync(
            page.SelectMany(r => r.Lines).Select(l => l.SkuId).Distinct().ToList(), cancellationToken);
        var names = await ReceiptUserNamesAsync(page, cancellationToken);
        return PagedResponse<ReceiptResponse>.Create(
            page.Select(r => ToResponse(r, skus, names)).ToList(), query, total);
    }

    public async Task<ReceiptResponse> GetReceiptAsync(long receiptId, CancellationToken cancellationToken)
    {
        var receipt = await dbContext.GoodsReceipts.AsNoTracking()
            .Include(r => r.Lines)
            .SingleOrDefaultAsync(r => r.Id == receiptId, cancellationToken)
            ?? throw InventoryErrors.NotFound("phiếu nhập");
        var skus = await skuReader.GetSkusAsync(receipt.Lines.Select(l => l.SkuId).Distinct().ToList(), cancellationToken);
        return ToResponse(receipt, skus, await ReceiptUserNamesAsync([receipt], cancellationToken));
    }

    /// <summary>Duyệt phiếu: tạo lô in, cộng tồn khả dụng, ghi thẻ kho (US-05).</summary>
    public async Task<ReceiptResponse> ApproveReceiptAsync(
        long approverId,
        long receiptId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Khóa dòng phiếu: hai người bấm duyệt cùng lúc thì người sau thấy phiếu đã duyệt, không nhập kho hai lần.
        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM inventory.goods_receipts WHERE id = {receiptId} FOR UPDATE", cancellationToken);
        var receipt = await dbContext.GoodsReceipts
            .Include(r => r.Lines)
            .SingleOrDefaultAsync(r => r.Id == receiptId, cancellationToken)
            ?? throw InventoryErrors.NotFound("phiếu nhập");
        receipt.Approve(approverId, now);
        await EnsureBatchCodesAvailableAsync(receipt, cancellationToken);

        var batches = receipt.Lines.Select(line => (Line: line, Batch: new PrintBatch(
            line.SkuId, line.BatchCode, line.PrinterName, line.Quantity, line.UnitCost, line.ReceivedAt))).ToList();
        dbContext.PrintBatches.AddRange(batches.Select(pair => pair.Batch));
        await dbContext.SaveChangesAsync(cancellationToken);

        var reference = ReceiptReference(receipt.Id);
        foreach (var (line, batch) in batches)
        {
            line.LinkBatch(batch.Id);
            dbContext.Transactions.Add(new InventoryTransaction(
                line.SkuId, batch.Id, TransactionType.Import, line.Quantity, reference, approverId));
            await IncreaseAvailableAsync(line.SkuId, line.Quantity, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("User {UserId} approved goods receipt {ReceiptId}", approverId, receiptId);
        return await GetReceiptAsync(receiptId, cancellationToken);
    }

    public async Task<ReceiptResponse> RejectReceiptAsync(
        long approverId,
        long receiptId,
        RejectReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var receipt = await dbContext.GoodsReceipts.SingleOrDefaultAsync(r => r.Id == receiptId, cancellationToken)
            ?? throw InventoryErrors.NotFound("phiếu nhập");
        receipt.Reject(approverId, request.Reason, timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("User {UserId} rejected goods receipt {ReceiptId}", approverId, receiptId);
        return await GetReceiptAsync(receiptId, cancellationToken);
    }

    public async Task<StockCardResponse> GetStockCardAsync(long skuId, CancellationToken cancellationToken)
    {
        var skus = await skuReader.GetSkusAsync([skuId], cancellationToken);
        if (!skus.TryGetValue(skuId, out var sku))
        {
            throw InventoryErrors.NotFound("SKU");
        }

        var stock = await dbContext.StockItems.AsNoTracking()
            .SingleOrDefaultAsync(s => s.SkuId == skuId, cancellationToken) ?? new StockItem(skuId);
        var batches = await dbContext.PrintBatches.AsNoTracking()
            .Where(batch => batch.SkuId == skuId)
            .OrderBy(batch => batch.ReceivedAt).ThenBy(batch => batch.Id)
            .Select(batch => new BatchResponse(
                batch.Id, batch.BatchCode, batch.PrinterName, batch.Quantity, batch.Remaining, batch.UnitCost,
                batch.ReceivedAt))
            .ToListAsync(cancellationToken);
        var transactions = await dbContext.Transactions.AsNoTracking()
            .Where(t => t.SkuId == skuId)
            .OrderByDescending(t => t.Id)
            .Take(RecentTransactionLimit)
            .ToListAsync(cancellationToken);
        var names = await userNames.GetNamesAsync(
            transactions.Where(t => t.CreatedBy is not null).Select(t => t.CreatedBy.GetValueOrDefault()).Distinct().ToList(),
            cancellationToken);

        return new StockCardResponse(
            skuId,
            sku.SkuCode,
            sku.BookTitle,
            sku.EditionName,
            sku.CoverType,
            stock.Available,
            stock.Reserved,
            stock.Available + stock.Reserved,
            stock.LowStockThreshold,
            batches,
            transactions.Select(t => new TransactionResponse(
                t.Id,
                t.Type,
                t.Quantity,
                t.PrintBatchId,
                t.Reference,
                t.CreatedBy,
                t.CreatedBy is { } creator ? names.GetValueOrDefault(creator) : null,
                t.Note,
                t.CreatedAt)).ToList());
    }

    /// <summary>SKU có tồn khả dụng ≤ ngưỡng (BR-50). Gửi email cảnh báo sẽ làm khi có module Notification.</summary>
    public async Task<PagedResponse<LowStockResponse>> ListLowStockAsync(
        PagedQuery query,
        CancellationToken cancellationToken)
    {
        var lowStock = dbContext.StockItems.AsNoTracking().Where(stock => stock.Available <= stock.LowStockThreshold);
        var total = await lowStock.CountAsync(cancellationToken);
        var page = await lowStock
            .OrderBy(stock => stock.Available)
            .ThenBy(stock => stock.SkuId)
            .Skip(query.Skip)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
        var skus = await skuReader.GetSkusAsync(page.Select(stock => stock.SkuId).ToList(), cancellationToken);

        var items = page.Select(stock => new LowStockResponse(
            stock.SkuId,
            skus.GetValueOrDefault(stock.SkuId)?.SkuCode,
            skus.GetValueOrDefault(stock.SkuId)?.BookTitle,
            stock.Available,
            stock.Reserved,
            stock.LowStockThreshold)).ToList();
        return PagedResponse<LowStockResponse>.Create(items, query, total);
    }

    /// <summary>Xuất kho thủ công (biếu tặng, hủy hàng lỗi) theo FIFO, bắt buộc lý do (BR-50).</summary>
    public async Task<StockCardResponse> IssueStockAsync(
        long userId,
        IssueStockRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reference = "ISSUE-" + now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var type = request.Type == IssueType.Gift ? TransactionType.Gift : TransactionType.Damage;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var affected = await dbContext.StockItems
            .Where(stock => stock.SkuId == request.SkuId && stock.Available >= request.Quantity)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(stock => stock.Available, stock => stock.Available - request.Quantity)
                    .SetProperty(stock => stock.UpdatedAt, now),
                cancellationToken);
        if (affected == 0)
        {
            throw InventoryErrors.OutOfStock();
        }

        await inventoryService.TakeFifoAsync(
            request.SkuId, request.Quantity, type, reference, request.Note.Trim(), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "User {UserId} issued {Quantity} of SKU {SkuId} as {IssueType}",
            userId, request.Quantity, request.SkuId, request.Type);

        return await GetStockCardAsync(request.SkuId, cancellationToken);
    }

    public async Task<StockCardResponse> SetThresholdAsync(
        long skuId,
        SetThresholdRequest request,
        CancellationToken cancellationToken)
    {
        var skus = await skuReader.GetSkusAsync([skuId], cancellationToken);
        if (!skus.ContainsKey(skuId))
        {
            throw InventoryErrors.NotFound("SKU");
        }

        var stock = await dbContext.StockItems.SingleOrDefaultAsync(s => s.SkuId == skuId, cancellationToken);
        if (stock is null)
        {
            stock = new StockItem(skuId);
            dbContext.StockItems.Add(stock);
        }

        stock.SetLowStockThreshold(request.LowStockThreshold);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetStockCardAsync(skuId, cancellationToken);
    }

    internal static string ReceiptReference(long receiptId) =>
        "GR-" + receiptId.ToString(CultureInfo.InvariantCulture);

    private async Task IncreaseAvailableAsync(long skuId, int quantity, DateTime now, CancellationToken cancellationToken)
    {
        // Upsert: SKU nhập lần đầu chưa có dòng stock_items.
        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO inventory.stock_items (sku_id, available, reserved, low_stock_threshold, created_at, updated_at)
            VALUES ({skuId}, {quantity}, 0, {StockItem.DefaultLowStockThreshold}, {now}, {now})
            ON CONFLICT (sku_id) DO UPDATE
            SET available = stock_items.available + EXCLUDED.available, updated_at = EXCLUDED.updated_at
            """,
            cancellationToken);
    }

    private async Task EnsureBatchCodesAvailableAsync(GoodsReceipt receipt, CancellationToken cancellationToken)
    {
        foreach (var line in receipt.Lines)
        {
            var exists = await dbContext.PrintBatches.AnyAsync(
                batch => batch.SkuId == line.SkuId && batch.BatchCode == line.BatchCode, cancellationToken);
            if (exists)
            {
                throw InventoryErrors.BatchCodeDuplicate(line.BatchCode);
            }
        }
    }

    private static void EnsureCanModify(GoodsReceipt receipt, long userId, bool isSuperAdmin)
    {
        if (receipt.CreatedBy != userId && !isSuperAdmin)
        {
            throw InventoryErrors.ReceiptForbidden();
        }
    }

    private static IEnumerable<GoodsReceiptLine> ToLines(CreateReceiptRequest request) =>
        request.Lines.Select(line => new GoodsReceiptLine(
            line.SkuId, line.BatchCode, line.PrinterName, line.Quantity, line.UnitCost, line.ReceivedAt));

    private async Task EnsureSkusExistAsync(CreateReceiptRequest request, CancellationToken cancellationToken)
    {
        var skuIds = request.Lines.Select(line => line.SkuId).Distinct().ToList();
        var skus = await skuReader.GetSkusAsync(skuIds, cancellationToken);
        if (skus.Count != skuIds.Count)
        {
            throw InventoryErrors.SkuNotFound();
        }
    }

    private Task<IReadOnlyDictionary<long, string>> ReceiptUserNamesAsync(
        IEnumerable<GoodsReceipt> receipts,
        CancellationToken cancellationToken) => userNames.GetNamesAsync(
            receipts.SelectMany(r => new long?[] { r.CreatedBy, r.DecidedBy })
                .OfType<long>()
                .Distinct()
                .ToList(),
            cancellationToken);

    private static ReceiptResponse ToResponse(
        GoodsReceipt receipt,
        IReadOnlyDictionary<long, SkuInfo> skus,
        IReadOnlyDictionary<long, string> names) => new(
        receipt.Id,
        receipt.Status,
        receipt.Note,
        receipt.CreatedBy,
        names.GetValueOrDefault(receipt.CreatedBy),
        receipt.CreatedAt,
        receipt.DecidedBy,
        receipt.DecidedBy is { } decidedBy ? names.GetValueOrDefault(decidedBy) : null,
        receipt.DecidedAt,
        receipt.RejectReason,
        receipt.Lines.OrderBy(line => line.Id).Select(line => new ReceiptLineResponse(
            line.Id,
            line.SkuId,
            skus.GetValueOrDefault(line.SkuId)?.SkuCode,
            skus.GetValueOrDefault(line.SkuId)?.BookTitle,
            skus.GetValueOrDefault(line.SkuId)?.EditionName,
            skus.GetValueOrDefault(line.SkuId)?.CoverType,
            line.BatchCode,
            line.PrinterName,
            line.Quantity,
            line.UnitCost,
            line.ReceivedAt,
            line.PrintBatchId)).ToList());
}
