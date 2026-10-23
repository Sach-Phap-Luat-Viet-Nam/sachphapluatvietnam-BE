namespace SachLuat.SharedKernel.Security;

/// <summary>Người dùng của request hiện tại; <see cref="UserId"/> null với khách vãng lai và job nền.</summary>
public interface ICurrentUser
{
    long? UserId { get; }

    bool IsAuthenticated => UserId is not null;

    bool IsInRole(string role);
}
