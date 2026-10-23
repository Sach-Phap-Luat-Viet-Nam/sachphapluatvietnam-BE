namespace SachLuat.Modules.Inventory.Domain;

// Lưu trong DB dạng snake_case (SnakeCaseEnumConverter): đổi tên member là đổi dữ liệu.

internal enum ReceiptStatus
{
    Draft,
    Approved,
    Rejected,
}

/// <summary>Loại dòng thẻ kho (mục 10.2): số lượng dương là nhập, âm là xuất.</summary>
internal enum TransactionType
{
    Import,
    Sale,
    Return,
    Adjust,
    Damage,
    Gift,
}

internal enum ReservationStatus
{
    Active,
    Released,
    Deducted,
    Returned,
}

/// <summary>Xuất kho thủ công (BR-50), bắt buộc ghi lý do.</summary>
internal enum IssueType
{
    Damage,
    Gift,
}
