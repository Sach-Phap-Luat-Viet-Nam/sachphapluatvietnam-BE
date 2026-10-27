using System.Globalization;
using SachLuat.Modules.Ordering.Domain;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>
/// Nội dung tin nhắn báo nhân viên. Tin đi qua nền tảng bên ngoài nên chỉ gửi đủ để nhận biết đơn: tên rút gọn,
/// SĐT che bớt, không có số nhà; chi tiết xem qua link Admin.
/// </summary>
internal static class OrderNotificationFormatter
{
    private const int MaxReasonLength = 200;

    public static string Format(OrderNotificationKind kind, Order order, string? adminBaseUrl)
    {
        var lines = new List<string>();
        var payment = PaymentLabel(order.PaymentMethod);
        lines.Add(kind switch
        {
            OrderNotificationKind.NewOrder => $"🛒 ĐƠN MỚI #{order.OrderCode} – {payment}",
            OrderNotificationKind.Paid => $"💰 ĐÃ THANH TOÁN #{order.OrderCode} – {payment}",
            _ => $"❌ KHÁCH HỦY ĐƠN #{order.OrderCode} – {payment}",
        });

        switch (order.OrderType)
        {
            case OrderType.Wholesale:
                lines.Add("🏷️ ĐƠN SỈ");
                break;
            case OrderType.Preorder:
                lines.Add("⏳ ĐƠN ĐẶT TRƯỚC");
                break;
        }

        lines.Add($"Khách: {ShortenName(order.CustomerName)} – {MaskPhone(order.Phone)}");
        lines.Add($"{order.Items.Sum(item => item.Quantity)} cuốn – Tổng: {FormatMoney(order.Total)}");
        lines.Add($"Giao: {order.ShippingAddress.Ward}, {order.ShippingAddress.Province}");

        if (kind == OrderNotificationKind.CancelledByCustomer
            && order.StatusLogs.Where(log => log.ToStatus == OrderStatus.Cancelled).MaxBy(log => log.CreatedAt)
                is { Note: { Length: > 0 } reason })
        {
            lines.Add(
                $"Lý do: {(reason.Length > MaxReasonLength ? string.Concat(reason[..MaxReasonLength], "…") : reason)}");
        }

        if (!string.IsNullOrWhiteSpace(adminBaseUrl))
        {
            lines.Add($"👉 {adminBaseUrl.TrimEnd('/')}/don-hang/{order.OrderCode}");
        }

        // Luôn dùng \n (không theo hệ điều hành) để tin nhắn hiển thị giống nhau ở mọi môi trường.
        return string.Join('\n', lines);
    }

    /// <summary>"Nguyễn Văn An" → "Nguyễn V. An": giữ họ và tên, rút gọn tên đệm.</summary>
    internal static string ShortenName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 2)
        {
            return string.Join(' ', parts);
        }

        return string.Join(
            ' ', parts[1..^1].Select(part => $"{part[0]}.").Prepend(parts[0]).Append(parts[^1]));
    }

    /// <summary>"0912345678" → "09xxxxx678".</summary>
    internal static string MaskPhone(string phone) =>
        phone.Length < 6
            ? new string('x', phone.Length)
            : $"{phone[..2]}{new string('x', phone.Length - 5)}{phone[^3..]}";

    internal static string FormatMoney(decimal amount) =>
        amount.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.') + "đ";

    private static string PaymentLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cod => "COD",
        PaymentMethod.BankTransfer => "Chuyển khoản",
        PaymentMethod.Vnpay => "VNPAY",
        PaymentMethod.Momo => "MoMo",
        PaymentMethod.Zalopay => "ZaloPay",
        _ => method.ToString(),
    };
}
