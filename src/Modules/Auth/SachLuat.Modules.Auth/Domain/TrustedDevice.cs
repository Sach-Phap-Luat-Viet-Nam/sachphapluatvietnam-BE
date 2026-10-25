namespace SachLuat.Modules.Auth.Domain;

/// <summary>
/// Thiết bị đã qua 2FA và được tin cậy tới <see cref="ExpiresAt"/>: đăng nhập bằng mật khẩu trên thiết bị này không phải
/// nhập lại mã. Chỉ lưu bản băm SHA-256 của token trong cookie, hạn cố định kể từ lần nhập mã (không gia hạn khi dùng).
/// </summary>
internal sealed class TrustedDevice
{
    public TrustedDevice(long userId, string tokenHash, DateTime createdAt, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public long Id { get; private set; }

    public long UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public bool IsActive(DateTime now) => ExpiresAt > now;
}
