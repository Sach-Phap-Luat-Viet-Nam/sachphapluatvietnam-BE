using SachLuat.Modules.Ordering.Domain;

namespace SachLuat.Modules.Ordering.Application;

internal static class OrderMapping
{
    public static OrderResponse ToResponse(this Order order) => new(
        order.OrderCode,
        order.Status,
        order.PaymentMethod,
        order.PaymentStatus,
        order.CustomerName,
        order.Phone,
        order.Email,
        new ShippingAddressDto(
            order.ShippingAddress.Province, order.ShippingAddress.Ward, order.ShippingAddress.StreetAddress),
        order.Items.OrderBy(item => item.Id).Select(item => new OrderItemResponse(
            item.SkuId,
            item.SkuCode,
            item.BookTitle,
            item.EditionName,
            item.Isbn,
            item.CoverType,
            item.UnitPrice,
            item.Quantity,
            item.LineTotal,
            item.DiscountAmount)).ToList(),
        order.Subtotal,
        order.Discount,
        order.ShippingFee,
        order.ShippingDiscount,
        order.Total,
        order.AppliedPromotions.Select(promotion => new AppliedPromotionResponse(
            promotion.PromotionId, promotion.Name, promotion.Kind, promotion.CouponCode, promotion.Amount)).ToList(),
        order.InvoiceRequired,
        order.InvoiceInfo is { } invoice
            ? new InvoiceInfoDto(invoice.CompanyName, invoice.TaxCode, invoice.Address, invoice.Email)
            : null,
        order.Note,
        order.PaymentExpiresAt,
        order.CanTransitionTo(OrderStatus.Cancelled),
        order.CreatedAt,
        order.StatusLogs
            .OrderBy(log => log.CreatedAt).ThenBy(log => log.Id)
            .Select(log => new OrderStatusLogResponse(log.FromStatus, log.ToStatus, log.Note, log.CreatedAt))
            .ToList());

    public static AdminOrderResponse ToAdminResponse(this Order order) => new(
        order.ToResponse(),
        order.UserId,
        OrderStatusMachine.NextStatuses(order.Status),
        order.Items.All(item => item.CostPrice is not null)
            ? order.Items.Sum(item => item.CostPrice.GetValueOrDefault() * item.Quantity)
            : null);
}
