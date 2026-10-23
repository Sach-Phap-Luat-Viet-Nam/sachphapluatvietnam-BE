using Microsoft.AspNetCore.Identity;
using SachLuat.Modules.Auth.Domain;

namespace SachLuat.Modules.Auth.Infrastructure;

/// <summary>Băm mật khẩu bằng bcrypt theo NFR-11 (thay PBKDF2 mặc định của Identity).</summary>
internal sealed class BCryptPasswordHasher : IPasswordHasher<AppUser>
{
    // Work factor 12 ≈ vài trăm ms mỗi lần băm: đủ chậm để chống dò, chưa ảnh hưởng đăng nhập.
    public const int WorkFactor = 12;

    public string HashPassword(AppUser user, string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public PasswordVerificationResult VerifyHashedPassword(AppUser user, string hashedPassword, string providedPassword)
    {
        if (!BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        return BCrypt.Net.BCrypt.PasswordNeedsRehash(hashedPassword, WorkFactor)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }
}
