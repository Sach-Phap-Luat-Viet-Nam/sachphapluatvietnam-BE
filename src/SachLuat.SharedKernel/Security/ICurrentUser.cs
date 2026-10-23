namespace SachLuat.SharedKernel.Security;

/// <summary>Người dùng của request hiện tại; <see cref="UserId"/> null với khách vãng lai và job nền.</summary>
public interface ICurrentUser
{
    long? UserId { get; }

    bool IsAuthenticated => UserId is not null;

    /// <summary>Id Super Admin đang fast login vào tài khoản này; null khi người dùng tự đăng nhập.</summary>
    long? ImpersonatorUserId { get; }

    bool IsImpersonated => ImpersonatorUserId is not null;

    bool IsInRole(string role);
}
