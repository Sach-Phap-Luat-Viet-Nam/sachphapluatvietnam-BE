using System.ComponentModel.DataAnnotations;

namespace SachLuat.Modules.Promotion.Application;

internal sealed class PromotionOptions
{
    public const string SectionName = "Promotion";

    /// <summary>
    /// Trần phần trăm cho chương trình giảm trên đơn. Nghị định về khuyến mại giới hạn mức giảm giá hàng hóa;
    /// mặc định 50% theo hiểu biết hiện tại, cần pháp chế xác nhận trước khi ra mắt.
    /// </summary>
    [Range(1, 100)]
    public int MaxDiscountPercent { get; set; } = 50;

    /// <summary>
    /// Trần tổng mức giảm (combo + chương trình giảm trên đơn) trên giá bán thường của sách thuộc combo, tính theo phần
    /// trăm. Chỉ chặn phần giảm của chương trình khi đã cộng dồn với combo; combo tự có trần riêng lúc tạo/sửa
    /// (<see cref="MaxDiscountPercent"/>). Cần pháp chế xác nhận cùng <see cref="MaxDiscountPercent"/>.
    /// </summary>
    [Range(1, 100)]
    public int MaxTotalDiscountPercent { get; set; } = 50;

    /// <summary>Số mã tối đa sinh trong một lần gọi.</summary>
    [Range(1, 10_000)]
    public int MaxCouponsPerBatch { get; set; } = 1000;
}
