namespace SachLuat.Modules.Cart.Infrastructure;

/// <summary>Lưu giỏ dạng skuId → số lượng; mỗi lần ghi gia hạn thời gian sống của giỏ.</summary>
internal interface ICartStore
{
    Task<IReadOnlyDictionary<long, int>> GetAsync(string cartKey);

    Task SetQuantityAsync(string cartKey, long skuId, int quantity);

    Task RemoveAsync(string cartKey, long skuId);

    Task DeleteAsync(string cartKey);
}
