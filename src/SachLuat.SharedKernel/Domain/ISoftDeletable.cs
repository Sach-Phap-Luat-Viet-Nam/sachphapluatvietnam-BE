namespace SachLuat.SharedKernel.Domain;

/// <summary>
/// Entity xóa mềm: <c>DbContext.Remove</c> chỉ gán <see cref="DeletedAt"/>, không xóa dòng.
/// Mỗi entity cần cấu hình query filter <c>DeletedAt == null</c>.
/// </summary>
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; }
}
