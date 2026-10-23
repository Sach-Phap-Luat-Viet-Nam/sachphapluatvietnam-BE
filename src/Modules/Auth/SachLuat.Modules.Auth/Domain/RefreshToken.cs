namespace SachLuat.Modules.Auth.Domain;

/// <summary>
/// Refresh token đã băm SHA-256 (không lưu token gốc). Mỗi lần refresh thì thu hồi token cũ và cấp token mới;
/// dùng lại token đã thu hồi bị coi là bị đánh cắp → thu hồi toàn bộ phiên của user.
/// </summary>
internal sealed class RefreshToken
{
    public RefreshToken(long userId, string tokenHash, DateTime expiresAt, bool isPersistent, bool isMultiFactor)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        IsPersistent = isPersistent;
        IsMultiFactor = isMultiFactor;
    }

    public long Id { get; private set; }

    public long UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    /// <summary>"Ghi nhớ đăng nhập" (FR-03): cookie lưu 30 ngày; ngược lại là cookie phiên.</summary>
    public bool IsPersistent { get; private set; }

    /// <summary>Phiên đã qua 2FA: access token cấp lại vẫn mang <c>amr=mfa</c>.</summary>
    public bool IsMultiFactor { get; private set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTime now) => RevokedAt ??= now;

    public void MarkCreated(DateTime now) => CreatedAt = now;
}
