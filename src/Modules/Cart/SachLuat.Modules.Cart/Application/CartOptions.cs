using System.ComponentModel.DataAnnotations;

namespace SachLuat.Modules.Cart.Application;

internal sealed class CartOptions
{
    public const string SectionName = "Cart";

    /// <summary>FR-31: tối đa 20 cuốn mỗi SKU trong đơn lẻ, vượt thì chuyển sang báo giá sỉ.</summary>
    [Range(1, 1000)]
    public int MaxQuantityPerSku { get; set; } = 20;

    [Range(1, 500)]
    public int MaxLines { get; set; } = 50;

    /// <summary>FR-30: giỏ lưu 30 ngày kể từ lần sửa cuối.</summary>
    [Range(1, 365)]
    public int LifetimeDays { get; set; } = 30;

    /// <summary>Tiền tố key Redis, để dùng chung Redis với ứng dụng khác mà không trùng key.</summary>
    [Required]
    public string KeyPrefix { get; set; } = "sachluat:cart:";
}
