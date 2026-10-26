using System.Text.Json.Serialization;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.SharedKernel.Api;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>Payload webhook SePay (chỉ các trường dùng tới; trường lạ bị bỏ qua).</summary>
/// <param name="TransferType"><c>in</c> là tiền vào, <c>out</c> là tiền ra.</param>
/// <param name="TransactionDate">Giờ Việt Nam, dạng <c>yyyy-MM-dd HH:mm:ss</c>.</param>
internal sealed record SepayWebhookPayload(
    long Id,
    string? Gateway,
    string? TransactionDate,
    string? AccountNumber,
    string? Code,
    string? Content,
    string? TransferType,
    decimal TransferAmount,
    string? ReferenceCode);

/// <summary>SePay coi webhook thành công khi nhận HTTP 200 kèm <c>success = true</c>.</summary>
internal sealed record SepayWebhookResponse(
    [property: JsonPropertyName("success")] bool Success);

/// <summary>Thông tin để khách chuyển khoản cho đơn đang chờ thanh toán.</summary>
/// <param name="TransferContent">Nội dung chuyển khoản, chính là mã đơn.</param>
internal sealed record PaymentInstructionsResponse(
    string BankName,
    string AccountNumber,
    string AccountName,
    decimal Amount,
    string TransferContent,
    string QrUrl);

internal sealed class AdminSepayTransactionsQuery : PagedQuery
{
    /// <summary>Lọc theo kết quả (snake_case: <c>unmatched</c>, <c>amount_mismatch</c>, ...).</summary>
    public string? Result { get; set; }
}

internal sealed record SepayTransactionResponse(
    long Id,
    long SepayId,
    string? Gateway,
    string? AccountNumber,
    string? Content,
    string? ReferenceCode,
    decimal Amount,
    DateTime? TransactionDate,
    string? OrderCode,
    SepayTransactionResult Result,
    string? Note,
    DateTime ReceivedAt);
