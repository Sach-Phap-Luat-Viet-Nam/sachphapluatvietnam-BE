using System.Globalization;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Cart.Application;
using StackExchange.Redis;

namespace SachLuat.Modules.Cart.Infrastructure;

/// <summary>Mỗi giỏ là một Redis hash: field = skuId, value = số lượng; TTL = <see cref="CartOptions.LifetimeDays"/>.</summary>
internal sealed class RedisCartStore(IConnectionMultiplexer redis, IOptions<CartOptions> options) : ICartStore
{
    private readonly TimeSpan _lifetime = TimeSpan.FromDays(options.Value.LifetimeDays);

    private IDatabase Database => redis.GetDatabase();

    public async Task<IReadOnlyDictionary<long, int>> GetAsync(string cartKey)
    {
        var entries = await Database.HashGetAllAsync(cartKey);
        var items = new Dictionary<long, int>(entries.Length);
        foreach (var entry in entries)
        {
            if (long.TryParse(entry.Name.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var skuId)
                && entry.Value.TryParse(out int quantity)
                && quantity > 0)
            {
                items[skuId] = quantity;
            }
        }

        return items;
    }

    public async Task SetQuantityAsync(string cartKey, long skuId, int quantity)
    {
        var batch = Database.CreateBatch();
        var set = batch.HashSetAsync(cartKey, skuId.ToString(CultureInfo.InvariantCulture), quantity);
        var expire = batch.KeyExpireAsync(cartKey, _lifetime);
        batch.Execute();
        await Task.WhenAll(set, expire);
    }

    public async Task RemoveAsync(string cartKey, long skuId)
    {
        await Database.HashDeleteAsync(cartKey, skuId.ToString(CultureInfo.InvariantCulture));
        await Database.KeyExpireAsync(cartKey, _lifetime);
    }

    public Task DeleteAsync(string cartKey) => Database.KeyDeleteAsync(cartKey);
}
