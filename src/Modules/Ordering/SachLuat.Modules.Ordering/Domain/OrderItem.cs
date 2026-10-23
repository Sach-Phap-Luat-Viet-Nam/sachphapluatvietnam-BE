namespace SachLuat.Modules.Ordering.Domain;

/// <summary>Dòng đơn: snapshot tên sách, ấn bản, ISBN, giá lúc đặt (mục 10.3).</summary>
internal sealed class OrderItem
{
    public OrderItem(
        long skuId,
        string skuCode,
        string bookTitle,
        string? editionName,
        string isbn,
        string coverType,
        decimal unitPrice,
        int quantity)
    {
        SkuId = skuId;
        SkuCode = skuCode;
        BookTitle = bookTitle;
        EditionName = editionName;
        Isbn = isbn;
        CoverType = coverType;
        UnitPrice = unitPrice;
        Quantity = quantity;
        LineTotal = unitPrice * quantity;
    }

    // EF Core
    private OrderItem()
    {
    }

    public long Id { get; private set; }

    public long OrderId { get; private set; }

    public long SkuId { get; private set; }

    public string SkuCode { get; private set; } = string.Empty;

    public string BookTitle { get; private set; } = string.Empty;

    public string? EditionName { get; private set; }

    public string Isbn { get; private set; } = string.Empty;

    public string CoverType { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal { get; private set; }

    /// <summary>Giá vốn bình quân mỗi cuốn theo các lô đã xuất (FIFO), có sau khi đóng gói.</summary>
    public decimal? CostPrice { get; private set; }

    public void SetCostPrice(decimal costPrice) => CostPrice = costPrice;
}

internal sealed class OrderStatusLog
{
    public OrderStatusLog(OrderStatus? fromStatus, OrderStatus toStatus, long? changedBy, string? note, DateTime createdAt)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedBy = changedBy;
        Note = note;
        CreatedAt = createdAt;
    }

    // EF Core
    private OrderStatusLog()
    {
    }

    public long Id { get; private set; }

    public long OrderId { get; private set; }

    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    /// <summary>Người đổi trạng thái; null là khách vãng lai hoặc hệ thống (job tự hủy).</summary>
    public long? ChangedBy { get; private set; }

    public string? Note { get; private set; }

    public DateTime CreatedAt { get; private set; }
}

/// <summary>Bộ đếm mã đơn theo ngày (giờ Việt Nam): SLVN + yyMMdd + số thứ tự.</summary>
internal sealed class OrderCodeCounter
{
    public DateOnly Day { get; private set; }

    public int LastValue { get; private set; }
}
