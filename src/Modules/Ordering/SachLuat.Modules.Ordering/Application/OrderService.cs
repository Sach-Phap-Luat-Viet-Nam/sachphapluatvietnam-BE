using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Cart.Contracts;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Errors;
using SachLuat.SharedKernel.Persistence;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>Đặt hàng và theo dõi đơn phía khách (FR-33, FR-40, FR-41).</summary>
internal sealed class OrderService(
    OrderingDbContext dbContext,
    OrderCodeGenerator codeGenerator,
    OrderStatusService statusService,
    ShippingFeeCalculator shippingFeeCalculator,
    ICurrentCart currentCart,
    ICatalogSkuReader skuReader,
    IInventoryService inventoryService,
    ICurrentUser currentUser,
    IOptions<OrderingOptions> options,
    TimeProvider timeProvider,
    ILogger<OrderService> logger)
{
    public const int IdempotencyKeyMaxLength = 64;

    // Giai đoạn 1 chỉ có COD và chuyển khoản đối soát tay; cổng thanh toán online làm ở giai đoạn tích hợp.
    private static readonly PaymentMethod[] SupportedPaymentMethods = [PaymentMethod.Cod, PaymentMethod.BankTransfer];

    private readonly OrderingOptions _options = options.Value;

    public async Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is { Length: > IdempotencyKeyMaxLength })
        {
            throw new BusinessException(
                ErrorCodes.ValidationError,
                $"Idempotency-Key tối đa {IdempotencyKeyMaxLength} ký tự",
                StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrEmpty(idempotencyKey) && await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
            is { } existing)
        {
            // Khách bấm đặt hàng nhiều lần: trả lại đơn đã tạo thay vì tạo đơn trùng (mục 11.1).
            return existing.UserId == currentUser.UserId && existing.Phone == request.Phone
                ? existing.ToResponse()
                : throw OrderErrors.IdempotencyKeyReused();
        }

        if (!SupportedPaymentMethods.Contains(request.PaymentMethod))
        {
            throw OrderErrors.PaymentMethodNotSupported();
        }

        var lines = await currentCart.GetLinesAsync(cancellationToken);
        if (lines.Count == 0)
        {
            throw OrderErrors.CartEmpty();
        }

        if (lines.Any(line => line.Quantity > _options.MaxQuantityPerSku))
        {
            throw OrderErrors.MaxQuantityExceeded(_options.MaxQuantityPerSku);
        }

        var skus = await skuReader.GetSkusAsync(lines.Select(line => line.SkuId).ToList(), cancellationToken);
        if (lines.Any(line => !skus.TryGetValue(line.SkuId, out var sku) || !sku.IsSellable))
        {
            throw OrderErrors.SkuNotAvailable();
        }

        var items = lines.Select(line =>
        {
            var sku = skus[line.SkuId];
            return new OrderItem(
                sku.SkuId, sku.SkuCode, sku.BookTitle, sku.EditionName, sku.Isbn, sku.CoverType, sku.SalePrice,
                line.Quantity);
        }).ToList();

        var orderCode = await codeGenerator.NextAsync(cancellationToken);
        var subtotal = items.Sum(item => item.LineTotal);
        var order = Order.Create(
            orderCode,
            currentUser.UserId,
            request.CustomerName.Trim(),
            request.Phone,
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            new ShippingAddress(
                request.ShippingAddress.Province.Trim(),
                request.ShippingAddress.Ward.Trim(),
                request.ShippingAddress.StreetAddress.Trim()),
            request.PaymentMethod,
            request.InvoiceInfo is { } invoice
                ? new InvoiceInfo(invoice.CompanyName.Trim(), invoice.TaxCode, invoice.Address.Trim(), invoice.Email)
                : null,
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            string.IsNullOrEmpty(idempotencyKey) ? null : idempotencyKey,
            items,
            shippingFeeCalculator.Calculate(subtotal),
            timeProvider.GetUtcNow().UtcDateTime,
            TimeSpan.FromMinutes(_options.PaymentTimeoutMinutes));

        // BR-11: tạm giữ tồn trước; thiếu hàng thì dừng ở đây (OUT_OF_STOCK), chưa có đơn nào được tạo.
        await inventoryService.ReserveAsync(
            orderCode, lines.Select(line => new StockLine(line.SkuId, line.Quantity)).ToList(), cancellationToken);
        try
        {
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Lưu đơn thất bại: trả lại tồn đã giữ (không dùng token của request để việc hoàn tồn luôn chạy xong).
            await inventoryService.ReleaseAsync(orderCode, CancellationToken.None);
            if (exception.IsUniqueViolation("ix_orders_idempotency_key"))
            {
                throw OrderErrors.IdempotencyKeyReused();
            }

            throw;
        }

        await currentCart.ClearAsync(cancellationToken);
        logger.LogInformation(
            "Created order {OrderCode} with {ItemCount} items, payment {PaymentMethod}",
            order.OrderCode, order.Items.Count, order.PaymentMethod);
        return order.ToResponse();
    }

    public async Task<PagedResponse<OrderSummaryResponse>> ListMineAsync(
        PagedQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var orders = dbContext.Orders.AsNoTracking().Where(order => order.UserId == userId);
        var total = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(order => order.CreatedAt)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Select(order => new OrderSummaryResponse(
                order.OrderCode,
                order.Status,
                order.PaymentMethod,
                order.PaymentStatus,
                order.CustomerName,
                order.Phone,
                order.Items.Sum(item => item.Quantity),
                order.Total,
                order.CreatedAt))
            .ToListAsync(cancellationToken);
        return PagedResponse<OrderSummaryResponse>.Create(items, query, total);
    }

    public async Task<OrderResponse> GetMineAsync(string orderCode, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var order = await LoadAsync(dbContext.Orders.Where(o => o.OrderCode == orderCode && o.UserId == userId), cancellationToken)
            ?? throw OrderErrors.NotFound();
        return order.ToResponse();
    }

    /// <summary>FR-40: tra cứu đơn không cần đăng nhập bằng mã đơn + số điện thoại.</summary>
    public async Task<OrderResponse> LookupAsync(LookupOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(
                dbContext.Orders.Where(o => o.OrderCode == request.OrderCode.Trim() && o.Phone == request.Phone),
                cancellationToken)
            ?? throw OrderErrors.NotFound();
        return order.ToResponse();
    }

    /// <summary>BR-12: khách tự hủy khi đơn chưa đóng gói.</summary>
    public async Task<OrderResponse> CancelMineAsync(
        string orderCode,
        CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var orderId = await dbContext.Orders.AsNoTracking()
            .Where(order => order.OrderCode == orderCode && order.UserId == userId)
            .Select(order => (long?)order.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw OrderErrors.NotFound();

        var order = await statusService.ChangeAsync(
            orderId,
            OrderStatus.Cancelled,
            userId,
            string.IsNullOrWhiteSpace(request.Reason) ? "Khách hủy đơn" : request.Reason.Trim(),
            guard: o =>
            {
                if (!o.CanTransitionTo(OrderStatus.Cancelled))
                {
                    throw OrderErrors.NotCancellable();
                }
            },
            cancellationToken);
        return order.ToResponse();
    }

    public ShippingQuoteResponse QuoteShipping(ShippingQuoteRequest request) => new(
        shippingFeeCalculator.Calculate(request.Subtotal), shippingFeeCalculator.FreeShippingThreshold);

    private Task<Order?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        LoadAsync(dbContext.Orders.Where(order => order.IdempotencyKey == idempotencyKey), cancellationToken);

    private static Task<Order?> LoadAsync(IQueryable<Order> orders, CancellationToken cancellationToken) => orders
        .AsNoTracking()
        .Include(order => order.Items)
        .Include(order => order.StatusLogs)
        .AsSplitQuery()
        .SingleOrDefaultAsync(cancellationToken);
}
