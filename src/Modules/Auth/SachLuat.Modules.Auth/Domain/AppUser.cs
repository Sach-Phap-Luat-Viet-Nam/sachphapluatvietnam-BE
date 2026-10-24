using Microsoft.AspNetCore.Identity;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Auth.Domain;

internal sealed class AppUser : IdentityUser<long>, IAuditable
{
    public const int AvatarUrlMaxLength = 500;

    public string FullName { get; set; } = string.Empty;

    public DateOnly? BirthDate { get; set; }

    /// <summary>Nghề nghiệp để gợi ý sách (FR-05).</summary>
    public Occupation? Occupation { get; set; }

    /// <summary>Giao diện sáng/tối khách đã chọn; null là chưa chọn (FE dùng mặc định tối).</summary>
    public ColorTheme? Theme { get; set; }

    /// <summary>Link ảnh đại diện trên Cloudinary; null là chưa có (FE hiện ảnh mặc định).</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Tăng mỗi lần kết thúc mọi phiên của tài khoản (xem <c>AuthClaimTypes.SessionVersion</c>). Chỉ tăng bằng câu
    /// UPDATE nguyên tử trong <c>AuthService</c>, không ghi qua <c>UserManager.UpdateAsync</c>.
    /// </summary>
    public int SessionVersion { get; set; }

    /// <summary>false: Super Admin đã vô hiệu hóa, không đăng nhập hay làm mới phiên được nữa.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }
}

/// <summary>Giao diện của storefront (JSON và CSDL: <c>dark</c>, <c>light</c>).</summary>
internal enum ColorTheme
{
    Dark,
    Light,
}

internal enum Occupation
{
    Student,
    Lawyer,
    Accountant,
    Other,
}
