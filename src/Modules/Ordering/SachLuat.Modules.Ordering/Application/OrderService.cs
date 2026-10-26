using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Cart.Contracts;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.Modules.Promotion.Contracts;
using SachLuat.Modules.Shipping.Contracts;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Domain;
using SachLuat.SharedKernel.Errors;
using SachLuat.SharedKernel.Persistence;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>Đặt hàng và theo dõi đơn phía khách (FR-33, FR-40, FR-41).</summary>
internal sealed class OrderService(
    OrderingDbContext dbContext,
    OrderCodeGenerator codeGenerator,
    OrderStatusService statusService,
    CheckoutPricingService pricingService,
    CustomerRiskService riskService,
    SepayPaymentService sepayService,
    IShippingQuoteService shippingQuoteService,
    IPromotionService promotionService,
    ICurrentCart currentCart,
    ICatalogSkuReader skuReader,
    IInventoryService inventoryService,
    ICurrentUser currentUser,
    IOptions<OrderingOptions> options,
    TimeProvider timeProvider,
    ILogger<OrderService> logger)
{
    public const int IdempotencyKeyMaxLength = 64;

    // COD và chuyển khoản (đối soát tự động qua webhook SePay); ví điện tử/cổng thanh toán khác chưa làm.
    private static readonly PaymentMethod[] SupportedPaymentMethods = [PaymentMethod.Cod, PaymentMethod.BankTransfer];

    private readonly OrderingOptions _options = options.Value;

    public async Task<OrderResponse> CreateAsync(
        CreateOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (await PrepareAsync(request, idempotencyKey, cancellationToken) is { } existing)
        {
            return existing;
        }

        var cartLines = await currentCart.GetLinesAsync(cancellationToken);
        if (cartLines.Count == 0)
        {
            throw OrderErrors.CartEmpty();
        }

        var order = await PlaceAsync(
            request,
            cartLines.Select(line => new StockLine(line.SkuId, line.Quantity)).ToList(),
            idempotencyKey,
            OrderErrors.SkuNotAvailable,
            cancellationToken);
        await currentCart.ClearAsync(cancellationToken);
        logger.LogInformation(
            "Created order {OrderCode} from cart with {ItemCount} items, payment {PaymentMethod}",
            order.OrderCode, order.Items.Count, order.PaymentMethod);
        return ToOrderResponse(order, includeGuestAccessToken: true);
    }

    /// <summary>"Mua ngay": đặt thẳng một SKU; giỏ hàng của khách giữ nguyên.</summary>
    public async Task<OrderResponse> BuyNowAsync(
        BuyNowOrderRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (await PrepareAsync(request, idempotencyKey, cancellationToken) is { } existing)
        {
            return existing;
        }

        var order = await PlaceAsync(
            request,
            [new StockLine(request.SkuId, request.Quantity)],
            idempotencyKey,
            OrderErrors.BuyNowSkuNotAvailable,
            cancellationToken);
        logger.LogInformation(
            "Created buy-now order {OrderCode} for SKU {SkuId} x{Quantity}, payment {PaymentMethod}",
            order.OrderCode, request.SkuId, request.Quantity, order.PaymentMethod);
        return ToOrderResponse(order, includeGuestAccessToken: true);
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
        return ToOrderResponse(order);
    }

    /// <summary>FR-40: tra cứu đơn không cần đăng nhập bằng mã đơn + số điện thoại.</summary>
    public async Task<OrderResponse> LookupAsync(LookupOrderRequest request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumbers.Normalize(request.Phone);
        var order = await LoadAsync(
                dbContext.Orders.Where(o => o.OrderCode == request.OrderCode.Trim() && o.Phone == phone),
                cancellationToken)
            ?? throw OrderErrors.NotFound();
        return ToOrderResponse(order);
    }

    /// <summary>Khách vãng lai xem đơn qua link bí mật nhận lúc đặt hàng.</summary>
    public async Task<OrderResponse> GetGuestAsync(string token, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(GuestOrder(token), cancellationToken) ?? throw OrderErrors.NotFound();
        return ToOrderResponse(order);
    }

    /// <summary>BR-12: khách tự hủy khi đơn chưa đóng gói.</summary>
    public async Task<OrderResponse> CancelMineAsync(
        string orderCode,
        CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var order = await CancelAsync(
            dbContext.Orders.Where(o => o.OrderCode == orderCode && o.UserId == userId),
            userId,
            request.Reason,
            "Khách hủy đơn",
            cancellationToken);
        return ToOrderResponse(order);
    }

    /// <summary>BR-12 cho khách vãng lai, xác thực bằng link bí mật thay cho đăng nhập.</summary>
    public async Task<OrderResponse> CancelGuestAsync(
        string token,
        CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await CancelAsync(
            GuestOrder(token), changedBy: null, request.Reason, "Khách vãng lai tự hủy", cancellationToken);
        return ToOrderResponse(order);
    }

    /// <summary>Phí ship tạm tính. Chỉ có <c>Subtotal</c> thì dùng phí cố định như trước khi có vùng giao hàng.</summary>
    public async Task<ShippingQuoteResponse> QuoteShippingAsync(
        ShippingQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var items = new List<ShippingItem>();
        if (request.Items is { Count: > 0 })
        {
            var skus = await skuReader.GetSkusAsync(
                [.. request.Items.Select(item => item.SkuId).Distinct()], cancellationToken);
            items.AddRange(request.Items
                .Where(item => skus.ContainsKey(item.SkuId))
                .Select(item => new ShippingItem(skus[item.SkuId].WeightGram, item.Quantity)));
        }

        var quote = await shippingQuoteService.QuoteAsync(request.Province, items, request.Subtotal, cancellationToken);
        return ToResponse(quote);
    }

    /// <summary>
    /// Xem trước tiền ở trang thanh toán (tạm tính, giảm giá, phí ship, tổng) cho giỏ hiện tại hoặc "Mua ngay".
    /// Cùng cách tính với lúc tạo đơn nhưng không ghi gì, không trừ lượt dùng mã.
    /// </summary>
    public async Task<CheckoutPreviewResponse> PreviewAsync(
        CheckoutPreviewRequest request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StockLine> lines;
        Func<BusinessException> skuNotAvailable;
        if (request.Source == CheckoutSource.Cart)
        {
            var cartLines = await currentCart.GetLinesAsync(cancellationToken);
            if (cartLines.Count == 0)
            {
                throw OrderErrors.CartEmpty();
            }

            lines = [.. cartLines.Select(line => new StockLine(line.SkuId, line.Quantity))];
            skuNotAvailable = OrderErrors.SkuNotAvailable;
        }
        else
        {
            lines = [new StockLine(request.SkuId.GetValueOrDefault(), request.Quantity.GetValueOrDefault())];
            skuNotAvailable = OrderErrors.BuyNowSkuNotAvailable;
        }

        var pricing = await pricingService.PriceAsync(
            lines,
            request.Province,
            request.CouponCode,
            new PromotionCustomer(currentUser.UserId, request.Phone),
            skuNotAvailable,
            cancellationToken);
        return new CheckoutPreviewResponse(
            [.. pricing.Lines.Select(line => new PricedItemResponse(
                line.Sku.SkuId,
                line.Sku.SkuCode,
                line.Sku.BookTitle,
                line.Sku.EditionName,
                line.Sku.CoverType,
                line.UnitPrice,
                line.Quantity,
                line.LineTotal,
                line.Discount))],
            pricing.Subtotal,
            pricing.Discount,
            pricing.Shipping.Fee,
            pricing.ShippingDiscount,
            pricing.Total,
            [.. pricing.ToCharges().AppliedPromotions.Select(promotion => new AppliedPromotionResponse(
                promotion.PromotionId, promotion.Name, promotion.Kind, promotion.CouponCode, promotion.Amount))],
            pricing.CouponApplied,
            pricing.Shipping.FreeShippingThreshold,
            pricing.Shipping.AmountToFreeShipping,
            ToEta(pricing.Shipping),
            pricing.Shipping.ZoneName,
            string.IsNullOrWhiteSpace(request.Phone)
                || (await riskService.GetAsync(request.Phone, cancellationToken)).CodAllowed);
    }

    private OrderResponse ToOrderResponse(Order order, bool includeGuestAccessToken = false) =>
        order.ToResponse(includeGuestAccessToken) with { PaymentInstructions = sepayService.BuildInstructions(order) };

    private static ShippingQuoteResponse ToResponse(ShippingQuote quote) => new(
        quote.Fee, quote.FreeShippingThreshold, quote.AmountToFreeShipping, ToEta(quote), quote.ZoneName);

    private static EtaResponse? ToEta(ShippingQuote quote) =>
        quote is { EtaMinDays: { } min, EtaMaxDays: { } max } ? new EtaResponse(min, max) : null;

    /// <summary>
    /// Kiểm tra chung trước khi đặt hàng; trả đơn đã tạo nếu <paramref name="idempotencyKey"/> đã được dùng.
    /// </summary>
    private async Task<OrderResponse?> PrepareAsync(
        ICheckoutDetails details,
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
            return existing.UserId == currentUser.UserId && existing.Phone == PhoneNumbers.Normalize(details.Phone)
                ? ToOrderResponse(existing, includeGuestAccessToken: true)
                : throw OrderErrors.IdempotencyKeyReused();
        }

        if (!SupportedPaymentMethods.Contains(details.PaymentMethod))
        {
            throw OrderErrors.PaymentMethodNotSupported();
        }

        if (details.PaymentMethod == PaymentMethod.Cod)
        {
            await riskService.EnsureCodAllowedAsync(details.Phone, cancellationToken);
        }

        return null;
    }

    /// <summary>Hủy đơn phía khách; <paramref name="orders"/> đã lọc đúng đơn của người gọi.</summary>
    private async Task<Order> CancelAsync(
        IQueryable<Order> orders,
        long? changedBy,
        string? reason,
        string defaultNote,
        CancellationToken cancellationToken)
    {
        var orderId = await orders.AsNoTracking()
            .Select(order => (long?)order.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw OrderErrors.NotFound();

        return await statusService.ChangeAsync(
            orderId,
            OrderStatus.Cancelled,
            changedBy,
            string.IsNullOrWhiteSpace(reason) ? defaultNote : reason.Trim(),
            guard: o =>
            {
                if (!o.CanTransitionTo(OrderStatus.Cancelled))
                {
                    throw OrderErrors.NotCancellable();
                }
            },
            cancellationToken);
    }

    // Token chỉ sinh cho đơn khách vãng lai nên không cần lọc thêm user_id.
    private IQueryable<Order> GuestOrder(string token) =>
        dbContext.Orders.Where(order => order.GuestAccessToken == token);

    /// <summary>Kiểm tra SKU, chụp snapshot, tạm giữ tồn và lưu đơn (mục 10.3, BR-11).</summary>
    private async Task<Order> PlaceAsync(
        ICheckoutDetails details,
        IReadOnlyList<StockLine> lines,
        string? idempotencyKey,
        Func<BusinessException> skuNotAvailable,
        CancellationToken cancellationToken)
    {
        var customer = new PromotionCustomer(currentUser.UserId, details.Phone);
        var pricing = await pricingService.PriceAsync(
            lines, details.ShippingAddress.Province, details.CouponCode, customer, skuNotAvailable, cancellationToken);

        // Khách chỉ trả đúng số tiền đã thấy: giá, chương trình hay mã đổi giữa chừng thì báo để họ xem lại.
        if (details.ExpectedTotal is { } expectedTotal && expectedTotal != pricing.Total)
        {
            throw OrderErrors.CheckoutTotalChanged();
        }

        var items = pricing.Lines.Select(line =>
        {
            var sku = line.Sku;
            var item = new OrderItem(
                sku.SkuId, sku.SkuCode, sku.BookTitle, sku.EditionName, sku.Isbn, sku.CoverType, line.UnitPrice,
                line.Quantity);
            item.ApplyDiscount(line.Discount);
            return item;
        }).ToList();

        var orderCode = await codeGenerator.NextAsync(cancellationToken);
        var order = Order.Create(
            orderCode,
            currentUser.UserId,
            details.CustomerName.Trim(),
            details.Phone,
            string.IsNullOrWhiteSpace(details.Email) ? null : details.Email.Trim(),
            new ShippingAddress(
                details.ShippingAddress.Province.Trim(),
                details.ShippingAddress.Ward.Trim(),
                details.ShippingAddress.StreetAddress.Trim()),
            details.PaymentMethod,
            details.InvoiceInfo is { } invoice
                ? new InvoiceInfo(invoice.CompanyName.Trim(), invoice.TaxCode, invoice.Address.Trim(), invoice.Email)
                : null,
            string.IsNullOrWhiteSpace(details.Note) ? null : details.Note.Trim(),
            string.IsNullOrEmpty(idempotencyKey) ? null : idempotencyKey,
            items,
            pricing.ToCharges(),
            timeProvider.GetUtcNow().UtcDateTime,
            TimeSpan.FromMinutes(_options.PaymentTimeoutMinutes));

        // BR-11: tạm giữ tồn trước; thiếu hàng thì dừng ở đây (OUT_OF_STOCK), chưa có đơn nào được tạo.
        await inventoryService.ReserveAsync(orderCode, lines, cancellationToken);

        // Hết lượt dùng mã giữa lúc khách đặt hàng: trả tồn đã giữ rồi báo lỗi rõ, không giảm sai.
        try
        {
            await promotionService.RedeemAsync(orderCode, pricing.Promotions, customer, cancellationToken);
        }
        catch (BusinessException)
        {
            await inventoryService.ReleaseAsync(orderCode, CancellationToken.None);
            throw;
        }

        try
        {
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Lưu đơn thất bại: trả lại tồn và lượt dùng mã đã giữ (không dùng token của request để việc hoàn
            // trả luôn chạy xong).
            await inventoryService.ReleaseAsync(orderCode, CancellationToken.None);
            await promotionService.ReleaseAsync(orderCode, CancellationToken.None);
            if (exception.IsUniqueViolation("ix_orders_idempotency_key"))
            {
                throw OrderErrors.IdempotencyKeyReused();
            }

            throw;
        }

        return order;
    }

    private Task<Order?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        LoadAsync(dbContext.Orders.Where(order => order.IdempotencyKey == idempotencyKey), cancellationToken);

    private static Task<Order?> LoadAsync(IQueryable<Order> orders, CancellationToken cancellationToken) => orders
        .AsNoTracking()
        .Include(order => order.Items)
        .Include(order => order.StatusLogs)
        .AsSplitQuery()
        .SingleOrDefaultAsync(cancellationToken);
}
