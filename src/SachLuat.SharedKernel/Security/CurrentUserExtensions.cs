using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.SharedKernel.Security;

public static class CurrentUserExtensions
{
    /// <summary>Dùng trong endpoint <c>[Authorize]</c>; null chỉ xảy ra khi cấu hình sai nên trả 401.</summary>
    public static long RequireUserId(this ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new BusinessException(
            ErrorCodes.Unauthorized, "Bạn cần đăng nhập", StatusCodes.Status401Unauthorized);
}
