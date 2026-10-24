using Microsoft.AspNetCore.Identity;
using SachLuat.SharedKernel.Domain;

namespace SachLuat.Modules.Auth.Domain;

internal sealed class AppUser : IdentityUser<long>, IAuditable
{
    public string FullName { get; set; } = string.Empty;

    public DateOnly? BirthDate { get; set; }

    /// <summary>Nghề nghiệp để gợi ý sách (FR-05).</summary>
    public Occupation? Occupation { get; set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Super Admin khóa tài khoản: khóa không thời hạn, khác tạm khóa 15 phút do đăng nhập sai.</summary>
    public bool IsDisabled => LockoutEnd == DisabledLockoutEnd;

    public static DateTimeOffset DisabledLockoutEnd => DateTimeOffset.MaxValue;
}

internal enum Occupation
{
    Student,
    Lawyer,
    Accountant,
    Other,
}
