using Microsoft.AspNetCore.Identity;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Auth.Domain;

internal sealed class AppUser : IdentityUser<long>, IAuditable
{
    public string FullName { get; set; } = string.Empty;

    public DateOnly? BirthDate { get; set; }

    /// <summary>Nghề nghiệp để gợi ý sách (FR-05).</summary>
    public Occupation? Occupation { get; set; }

    /// <summary>Giao diện sáng/tối khách đã chọn; null là chưa chọn (FE dùng mặc định tối).</summary>
    public ColorTheme? Theme { get; set; }

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
