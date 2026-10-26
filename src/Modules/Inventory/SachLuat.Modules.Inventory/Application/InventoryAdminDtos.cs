using FluentValidation;
using SachLuat.Modules.Inventory.Domain;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Persistence;

namespace SachLuat.Modules.Inventory.Application;

internal sealed record ReceiptLineRequest(
    long SkuId,
    string BatchCode,
    string? PrinterName,
    int Quantity,
    decimal UnitCost,
    DateOnly ReceivedAt);

internal sealed record CreateReceiptRequest(string? Note, IReadOnlyList<ReceiptLineRequest> Lines);

internal sealed record RejectReceiptRequest(string Reason);

internal sealed record IssueStockRequest(long SkuId, int Quantity, IssueType Type, string Note);

internal sealed record SetThresholdRequest(int LowStockThreshold);

internal sealed class SkuSearchQuery
{
    public const int DefaultLimit = 8;
    public const int MaxLimit = 20;

    /// <summary>Một phần tên sách, mã SKU hoặc ISBN.</summary>
    public string Q { get; set; } = string.Empty;

    public int Limit { get; set; } = DefaultLimit;
}

/// <summary>Một SKU trong kết quả tìm để chọn nhanh khi lập phiếu nhập; <c>Available</c> là tồn khả dụng hiện tại.</summary>
internal sealed record SkuSearchResponse(
    long SkuId,
    string SkuCode,
    string BookTitle,
    string? EditionName,
    string CoverType,
    string Isbn,
    bool IsLatestEdition,
    int Available);

internal sealed class ReceiptsQuery : PagedQuery
{
    /// <summary><c>draft</c>, <c>approved</c>, <c>rejected</c>.</summary>
    public string? Status { get; set; }
}

internal sealed record ReceiptLineResponse(
    long Id,
    long SkuId,
    string? SkuCode,
    string? BookTitle,
    string? EditionName,
    string? CoverType,
    string BatchCode,
    string? PrinterName,
    int Quantity,
    decimal UnitCost,
    DateOnly ReceivedAt,
    long? PrintBatchId);

internal sealed record ReceiptResponse(
    long Id,
    ReceiptStatus Status,
    string? Note,
    long CreatedBy,
    string? CreatedByName,
    DateTime CreatedAt,
    long? DecidedBy,
    string? DecidedByName,
    DateTime? DecidedAt,
    string? RejectReason,
    IReadOnlyList<ReceiptLineResponse> Lines);

internal sealed record BatchResponse(
    long Id,
    string BatchCode,
    string? PrinterName,
    int Quantity,
    int Remaining,
    decimal UnitCost,
    DateOnly ReceivedAt);

internal sealed record TransactionResponse(
    long Id,
    TransactionType Type,
    int Quantity,
    long? PrintBatchId,
    string Reference,
    long? CreatedBy,
    string? CreatedByName,
    string? Note,
    DateTime CreatedAt);

/// <summary>Thẻ kho của SKU (FR-80, US-05): tồn, các lô, lịch sử nhập/xuất gần nhất.</summary>
internal sealed record StockCardResponse(
    long SkuId,
    string? SkuCode,
    string? BookTitle,
    string? EditionName,
    string? CoverType,
    int Available,
    int Reserved,
    int OnHand,
    int LowStockThreshold,
    IReadOnlyList<BatchResponse> Batches,
    IReadOnlyList<TransactionResponse> RecentTransactions);

internal sealed record LowStockResponse(
    long SkuId,
    string? SkuCode,
    string? BookTitle,
    int Available,
    int Reserved,
    int LowStockThreshold);

internal sealed class CreateReceiptRequestValidator : AbstractValidator<CreateReceiptRequest>
{
    public CreateReceiptRequestValidator()
    {
        RuleFor(request => request.Note).MaximumLength(1000);
        RuleFor(request => request.Lines).NotEmpty();
        RuleForEach(request => request.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.SkuId).GreaterThan(0);
            line.RuleFor(l => l.BatchCode).NotEmpty().MaximumLength(50);
            line.RuleFor(l => l.PrinterName).MaximumLength(255);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0).LessThanOrEqualTo(999_999_999_999m)
                .Must(cost => decimal.Truncate(cost) == cost).WithMessage("Giá vốn phải là số nguyên VNĐ");
        });
    }
}

internal sealed class SkuSearchQueryValidator : AbstractValidator<SkuSearchQuery>
{
    public SkuSearchQueryValidator()
    {
        RuleFor(query => query.Q).NotEmpty().MaximumLength(100);
        RuleFor(query => query.Limit).InclusiveBetween(1, SkuSearchQuery.MaxLimit);
    }
}

internal sealed class RejectReceiptRequestValidator : AbstractValidator<RejectReceiptRequest>
{
    public RejectReceiptRequestValidator() => RuleFor(request => request.Reason).NotEmpty().MaximumLength(1000);
}

internal sealed class IssueStockRequestValidator : AbstractValidator<IssueStockRequest>
{
    public IssueStockRequestValidator()
    {
        RuleFor(request => request.SkuId).GreaterThan(0);
        RuleFor(request => request.Quantity).GreaterThan(0);
        RuleFor(request => request.Type).IsInEnum();
        RuleFor(request => request.Note).NotEmpty().MaximumLength(1000).WithMessage("Bắt buộc ghi lý do xuất kho");
    }
}

internal sealed class SetThresholdRequestValidator : AbstractValidator<SetThresholdRequest>
{
    public SetThresholdRequestValidator() => RuleFor(request => request.LowStockThreshold).InclusiveBetween(0, 100_000);
}

internal sealed class ReceiptsQueryValidator : PagedQueryValidator<ReceiptsQuery>
{
    public ReceiptsQueryValidator() => RuleFor(query => query.Status)
        .Must(value => SnakeCaseEnumConverter<ReceiptStatus>.TryParse(value, out _))
        .When(query => query.Status is not null)
        .WithMessage("Trạng thái không hợp lệ");
}
