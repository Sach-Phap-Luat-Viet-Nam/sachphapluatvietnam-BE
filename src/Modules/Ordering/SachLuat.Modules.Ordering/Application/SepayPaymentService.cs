using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Ordering.Domain;
using SachLuat.Modules.Ordering.Infrastructure.Persistence;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Errors;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Ordering.Application;

/// <summary>
/// Thanh toán chuyển khoản qua SePay: nhận webhook tiền vào, khớp mã đơn trong nội dung chuyển khoản và tự chuyển
/// đơn <c>pending_payment</c> sang <c>paid</c> khi đủ tiền. Mọi trường hợp khác chỉ được ghi nhận để Admin đối soát.
/// </summary>
internal sealed partial class SepayPaymentService(
    OrderingDbContext dbContext,
    OrderStatusService statusService,
    IOptions<SepayOptions> options,
    TimeProvider timeProvider,
    ILogger<SepayPaymentService> logger)
{
    private const string AuthScheme = "Apikey";
    private const string IncomingTransfer = "in";
    private const int MaxTextLength = 1000;

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly SepayOptions _options = options.Value;

    /// <summary>Header đúng dạng <c>Authorization: Apikey {key}</c>; chưa cấu hình key thì từ chối tất cả.</summary>
    public void EnsureAuthorized(string? authorizationHeader)
    {
        var prefix = AuthScheme + " ";
        if (string.IsNullOrEmpty(_options.ApiKey)
            || authorizationHeader is null
            || !authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !SecretEquals(authorizationHeader[prefix.Length..].Trim(), _options.ApiKey))
        {
            throw OrderErrors.SepayUnauthorized();
        }
    }

    public async Task HandleWebhookAsync(SepayWebhookPayload payload, CancellationToken cancellationToken)
    {
        if (!string.Equals(payload.TransferType, IncomingTransfer, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var transaction = await dbContext.SepayTransactions
            .SingleOrDefaultAsync(t => t.SepayId == payload.Id, cancellationToken);
        if (transaction is null)
        {
            transaction = new SepayTransaction(
                payload.Id,
                Truncate(payload.Gateway),
                Truncate(payload.AccountNumber),
                Truncate(payload.Content),
                Truncate(payload.Code),
                Truncate(payload.ReferenceCode),
                payload.TransferAmount,
                ParseTransactionDate(payload.TransactionDate),
                timeProvider.GetUtcNow().UtcDateTime);
            dbContext.SepayTransactions.Add(transaction);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsUniqueViolation())
            {
                // Hai lần gửi cùng giao dịch chạy song song: lần kia đang xử lý.
                dbContext.Entry(transaction).State = EntityState.Detached;
                logger.LogInformation("SePay transaction {SepayId} is already being handled", payload.Id);
                return;
            }
        }
        else if (transaction.Result != SepayTransactionResult.Pending)
        {
            logger.LogInformation("Ignored duplicate SePay transaction {SepayId}", payload.Id);
            return;
        }

        await ResolveAsync(transaction, cancellationToken);
    }

    public async Task<PagedResponse<SepayTransactionResponse>> ListTransactionsAsync(
        AdminSepayTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var transactions = dbContext.SepayTransactions.AsNoTracking();
        if (SnakeCaseEnumConverter<SepayTransactionResult>.TryParse(query.Result, out var result))
        {
            transactions = transactions.Where(transaction => transaction.Result == result);
        }

        var total = await transactions.CountAsync(cancellationToken);
        var items = await transactions
            .OrderByDescending(transaction => transaction.ReceivedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip(query.Skip)
            .Take(query.Limit)
            .Select(transaction => new SepayTransactionResponse(
                transaction.Id,
                transaction.SepayId,
                transaction.Gateway,
                transaction.AccountNumber,
                transaction.Content,
                transaction.ReferenceCode,
                transaction.Amount,
                transaction.TransactionDate,
                transaction.OrderCode,
                transaction.Result,
                transaction.Note,
                transaction.ReceivedAt))
            .ToListAsync(cancellationToken);
        return PagedResponse<SepayTransactionResponse>.Create(items, query, total);
    }

    /// <summary>Chỉ đơn chuyển khoản còn chờ thanh toán mới có hướng dẫn; chưa cấu hình tài khoản thì null.</summary>
    public PaymentInstructionsResponse? BuildInstructions(Order order)
    {
        if (!_options.HasReceivingAccount
            || order.PaymentMethod != PaymentMethod.BankTransfer
            || order.Status != OrderStatus.PendingPayment)
        {
            return null;
        }

        var amount = order.Total.ToString("0", CultureInfo.InvariantCulture);
        var qrUrl = $"{_options.QrBaseUrl}?acc={Uri.EscapeDataString(_options.AccountNumber)}"
            + $"&bank={Uri.EscapeDataString(_options.BankName)}"
            + $"&amount={amount}&des={Uri.EscapeDataString(order.OrderCode)}";
        return new PaymentInstructionsResponse(
            _options.BankName, _options.AccountNumber, _options.AccountName, order.Total, order.OrderCode, qrUrl);
    }

    private async Task ResolveAsync(SepayTransaction transaction, CancellationToken cancellationToken)
    {
        var orderCode = ExtractOrderCode(transaction.Code) ?? ExtractOrderCode(transaction.Content);
        if (orderCode is null)
        {
            await FinishAsync(
                transaction,
                SepayTransactionResult.Unmatched,
                null,
                "Không thấy mã đơn trong nội dung",
                cancellationToken);
            return;
        }

        var order = await dbContext.Orders.AsNoTracking()
            .Where(o => o.OrderCode == orderCode)
            .Select(o => new { o.Id, o.PaymentMethod, o.Status, o.Total })
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null || order.PaymentMethod != PaymentMethod.BankTransfer)
        {
            await FinishAsync(
                transaction,
                SepayTransactionResult.Unmatched,
                orderCode,
                "Mã đơn không tồn tại hoặc không phải đơn chuyển khoản",
                cancellationToken);
            return;
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            await FinishAsync(
                transaction,
                SepayTransactionResult.OrderNotPayable,
                orderCode,
                $"Đơn đang ở trạng thái {SnakeCaseEnumConverter<OrderStatus>.ToProvider(order.Status)}",
                cancellationToken);
            return;
        }

        if (transaction.Amount != order.Total)
        {
            await FinishAsync(
                transaction,
                SepayTransactionResult.AmountMismatch,
                orderCode,
                string.Create(
                    CultureInfo.InvariantCulture, $"Nhận {transaction.Amount:0}, đơn cần {order.Total:0}"),
                cancellationToken);
            return;
        }

        try
        {
            await statusService.ChangeAsync(
                order.Id,
                OrderStatus.Paid,
                changedBy: null,
                $"SePay xác nhận đã nhận tiền (giao dịch {transaction.SepayId})",
                guard: null,
                cancellationToken);
        }
        catch (BusinessException exception) when (exception.Code == OrderErrors.InvalidTransitionCode)
        {
            // Đơn vừa bị hủy/thanh toán ngay trước khi khóa được dòng đơn.
            await FinishAsync(
                transaction,
                SepayTransactionResult.OrderNotPayable,
                orderCode,
                "Đơn đổi trạng thái trong lúc xử lý",
                cancellationToken);
            return;
        }

        await FinishAsync(transaction, SepayTransactionResult.Matched, orderCode, null, cancellationToken);
        logger.LogInformation("Order {OrderCode} paid by SePay transaction {SepayId}", orderCode, transaction.SepayId);
    }

    private async Task FinishAsync(
        SepayTransaction transaction,
        SepayTransactionResult result,
        string? orderCode,
        string? note,
        CancellationToken cancellationToken)
    {
        transaction.Resolve(result, orderCode, note);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (result != SepayTransactionResult.Matched)
        {
            // Cần người xử lý (hoàn tiền, ghép đơn tay): để mức Warning cho dễ cảnh báo.
            logger.LogWarning(
                "SePay transaction {SepayId} needs manual review: {Result} for order {OrderCode}",
                transaction.SepayId, result, orderCode);
        }
    }

    internal static string? ExtractOrderCode(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var match = OrderCodePattern().Match(text);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    // SePay trả giờ Việt Nam không kèm múi giờ; đổi về UTC như các cột thời gian khác.
    private static DateTime? ParseTransactionDate(string? value) =>
        DateTime.TryParseExact(
            value,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var local)
            ? new DateTimeOffset(local, VietnamOffset).UtcDateTime
            : null;

    private static string? Truncate(string? value) =>
        value is { Length: > MaxTextLength } ? value[..MaxTextLength] : value;

    // So sánh qua hash để thời gian chạy không phụ thuộc độ dài/vị trí ký tự khác nhau đầu tiên.
    private static bool SecretEquals(string provided, string expected) => CryptographicOperations.FixedTimeEquals(
        SHA256.HashData(Encoding.UTF8.GetBytes(provided)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    // Mã đơn: tiền tố + yyMMdd + số thứ tự (OrderCodeGenerator); ngân hàng có thể đổi hoa/thường nội dung.
    [GeneratedRegex("SLVN[0-9]{10,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrderCodePattern();
}
