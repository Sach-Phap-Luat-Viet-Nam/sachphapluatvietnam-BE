namespace SachLuat.SharedKernel.Domain;

/// <summary>
/// Entity có <c>created_at</c>/<c>updated_at</c>, được <c>AuditableEntityInterceptor</c> tự gán (UTC) khi lưu.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }
}
