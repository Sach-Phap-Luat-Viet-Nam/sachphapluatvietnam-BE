namespace SachLuat.Modules.Ordering.Domain;

// Lưu trong DB và trả API dạng snake_case (pending_payment, bank_transfer, ...), khớp mục 10.2 và 16.1.

internal enum OrderStatus
{
    PendingPayment,
    PendingConfirm,
    PreorderWaiting,
    Paid,
    Confirmed,
    Packing,
    ReadyToShip,
    Shipping,
    Delivered,
    Completed,
    ReturnRequested,
    Returning,
    Returned,
    Refunded,
    Cancelled,
}

internal enum PaymentMethod
{
    Cod,
    Vnpay,
    Momo,
    Zalopay,
    BankTransfer,
}

internal enum PaymentStatus
{
    Unpaid,
    Paid,
    Refunded,
    PartialRefund,
}

internal enum OrderType
{
    Retail,
    Wholesale,
    Preorder,
}
