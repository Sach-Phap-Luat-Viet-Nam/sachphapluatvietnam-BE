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
    private const int MaxTitleLength = 60;

    // Đơn nhiều đầu sách thì chỉ liệt kê vài dòng đầu cho tin gọn, phần còn lại xem trên Admin.
    private const int MaxItemLines = 5;

    private const string Divider = "━━━━━━━━━━━━";

    public static string Format(OrderNotificationKind kind, Order order, string? adminBaseUrl)
    {
        // Dòng đầu là thứ hiện trong thông báo đẩy của Zalo nên phải đủ để biết chuyện gì, đơn nào.
        var lines = new List<string>
        {
            kind switch
            {
                OrderNotificationKind.NewOrder => $"🛒 ĐƠN MỚI #{order.OrderCode}",
                OrderNotificationKind.Paid => $"💰 ĐÃ THANH TOÁN #{order.OrderCode}",
                _ => $"❌ KHÁCH HỦY ĐƠN #{order.OrderCode}",
            },
            $"💵 {FormatMoney(order.Total)} · {PaymentLabel(order.PaymentMethod)} · "
            + $"{order.Items.Sum(item => item.Quantity)} cuốn",
        };

        switch (order.OrderType)
        {
            case OrderType.Wholesale:
                lines.Add("🏷️ ĐƠN SỈ");
                break;
            case OrderType.Preorder:
                lines.Add("⏳ ĐƠN ĐẶT TRƯỚC");
                break;
        }

        lines.Add(Divider);
        lines.Add($"👤 {ShortenName(order.CustomerName)} · {MaskPhone(order.Phone)}");
        lines.Add($"📍 {order.ShippingAddress.Ward}, {order.ShippingAddress.Province}");
        lines.Add("📚 Sách:");
        lines.AddRange(ItemLines(order.Items));

        if (kind == OrderNotificationKind.CancelledByCustomer
            && order.StatusLogs.Where(log => log.ToStatus == OrderStatus.Cancelled).MaxBy(log => log.CreatedAt)
                is { Note: { Length: > 0 } reason })
        {
            lines.Add($"📝 Lý do: {Shorten(reason, MaxReasonLength)}");
        }

        lines.Add(Divider);
        lines.Add(NextStep(kind, order));
        if (!string.IsNullOrWhiteSpace(adminBaseUrl))
        {
            lines.Add($"🔗 Xem đơn: {adminBaseUrl.TrimEnd('/')}/don-hang/{order.OrderCode}");
        }

        // Luôn dùng \n (không theo hệ điều hành) để tin nhắn hiển thị giống nhau ở mọi môi trường.
        return string.Join('\n', lines);
    }

    private static IEnumerable<string> ItemLines(IReadOnlyCollection<OrderItem> items)
    {
        var shown = items.Count <= MaxItemLines ? items.Count : MaxItemLines - 1;
        foreach (var item in items.Take(shown))
        {
            var title = string.IsNullOrWhiteSpace(item.EditionName)
                ? item.BookTitle
                : $"{item.BookTitle} ({item.EditionName})";
            yield return $"• {Shorten(title, MaxTitleLength)} ×{item.Quantity}";
        }

        if (items.Count > shown)
        {
            yield return $"• … và {items.Count - shown} sách khác";
        }
    }

    private static string NextStep(OrderNotificationKind kind, Order order) => kind switch
    {
        OrderNotificationKind.NewOrder => "➡️ Gọi khách xác nhận đơn",
        OrderNotificationKind.Paid => "➡️ Xác nhận và đóng gói đơn",
        // Khách được hủy cả đơn chuyển khoản đã xác nhận (BR-12); khi đó tiền đã về, phải hoàn tay.
        _ when order.PaymentStatus == PaymentStatus.Paid => "⚠️ Khách đã chuyển khoản: cần hoàn tiền",
        _ => "ℹ️ Tồn kho đã tự hoàn lại, không cần xử lý thêm",
    };

    private static string Shorten(string text, int maxLength) =>
        text.Length > maxLength ? string.Concat(text.AsSpan(0, maxLength), "…") : text;

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
