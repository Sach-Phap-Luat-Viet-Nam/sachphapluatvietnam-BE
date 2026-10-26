using System.ComponentModel.DataAnnotations;

namespace SachLuat.Modules.Ordering.Application;

internal sealed class SepayOptions
{
    public const string SectionName = "SePay";

    /// <summary>
    /// Khóa SePay gửi trong header <c>Authorization: Apikey ...</c> của webhook. Để trống thì webhook từ chối mọi
    /// request (fail closed).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Số tài khoản nhận tiền đã liên kết với SePay. Trống thì không trả thông tin chuyển khoản.</summary>
    [MaxLength(30)]
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Tên ngân hàng dùng dựng mã QR (ví dụ <c>MBBank</c>), theo danh sách của SePay.</summary>
    [MaxLength(50)]
    public string BankName { get; set; } = string.Empty;

    /// <summary>Tên chủ tài khoản, chỉ để hiển thị cho khách.</summary>
    [MaxLength(100)]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Địa chỉ ảnh VietQR của SePay.</summary>
    [Url]
    public string QrBaseUrl { get; set; } = "https://qr.sepay.vn/img";

    public bool HasReceivingAccount =>
        !string.IsNullOrWhiteSpace(AccountNumber) && !string.IsNullOrWhiteSpace(BankName);
}
