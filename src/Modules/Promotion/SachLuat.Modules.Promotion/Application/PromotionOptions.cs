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

    /// <summary>Số mã tối đa sinh trong một lần gọi.</summary>
    [Range(1, 10_000)]
    public int MaxCouponsPerBatch { get; set; } = 1000;
}
