namespace SachLuat.SharedKernel.Security;

/// <summary>Đọc tên hiển thị của người dùng cho module khác (người lập/duyệt phiếu, người thao tác).</summary>
public interface IUserNameReader
{
    /// <summary>Tên theo id; id không tồn tại hoặc đã xóa thì không có trong kết quả.</summary>
    Task<IReadOnlyDictionary<long, string>> GetNamesAsync(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken);
}
