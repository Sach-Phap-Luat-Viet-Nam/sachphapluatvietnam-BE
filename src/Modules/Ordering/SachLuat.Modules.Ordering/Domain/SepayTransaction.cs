namespace SachLuat.Modules.Ordering.Domain;

internal enum SepayTransactionResult
{
    /// <summary>Đã nhận webhook, chưa xử lý xong (gặp lỗi giữa chừng thì SePay gửi lại sẽ xử lý tiếp).</summary>
    Pending,

    /// <summary>Khớp đơn, đủ tiền và đơn đã chuyển sang <c>paid</c>.</summary>
    Matched,

    /// <summary>Không tìm thấy mã đơn trong nội dung, hoặc mã không thuộc đơn chuyển khoản nào.</summary>
    Unmatched,

    /// <summary>Khớp đơn nhưng số tiền khác tổng đơn (thiếu hoặc thừa).</summary>
    AmountMismatch,

    /// <summary>Khớp đơn nhưng đơn không còn chờ thanh toán (đã hủy, quá hạn, đã thanh toán).</summary>
    OrderNotPayable,
}

/// <summary>
/// Một giao dịch tiền vào do SePay báo qua webhook. Lưu mọi giao dịch (kể cả không khớp đơn) để Admin đối soát tay;
/// <see cref="SepayId"/> unique để SePay gửi lại cùng giao dịch không bị xử lý hai lần.
/// </summary>
internal sealed class SepayTransaction
{
    public SepayTransaction(
        long sepayId,
        string? gateway,
        string? accountNumber,
        string? content,
        string? code,
        string? referenceCode,
        decimal amount,
        DateTime? transactionDate,
        DateTime receivedAt)
    {
        SepayId = sepayId;
        Gateway = gateway;
        AccountNumber = accountNumber;
        Content = content;
        Code = code;
        ReferenceCode = referenceCode;
        Amount = amount;
        TransactionDate = transactionDate;
        ReceivedAt = receivedAt;
        Result = SepayTransactionResult.Pending;
    }

    // EF Core
    private SepayTransaction()
    {
    }

    public long Id { get; private set; }

    /// <summary>Mã giao dịch phía SePay (<c>id</c> trong payload).</summary>
    public long SepayId { get; private set; }

    public string? Gateway { get; private set; }

    public string? AccountNumber { get; private set; }

    public string? Content { get; private set; }

    public string? Code { get; private set; }

    public string? ReferenceCode { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>Thời điểm giao dịch tại ngân hàng (UTC).</summary>
    public DateTime? TransactionDate { get; private set; }

    /// <summary>Mã đơn tách được từ nội dung chuyển khoản (kể cả khi không có đơn tương ứng).</summary>
    public string? OrderCode { get; private set; }

    public SepayTransactionResult Result { get; private set; }

    public string? Note { get; private set; }

    public DateTime ReceivedAt { get; private set; }

    public void Resolve(SepayTransactionResult result, string? orderCode, string? note)
    {
        Result = result;
        OrderCode = orderCode;
        Note = note;
    }
}
