using Microsoft.Extensions.Options;
using SachLuat.Modules.Cart.Contracts;
using SachLuat.Modules.Cart.Infrastructure;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Inventory.Contracts;

namespace SachLuat.Modules.Cart.Application;

internal sealed class CartService(
    ICartStore store,
    CartKeyResolver keyResolver,
    ICatalogSkuReader skuReader,
    IInventoryService inventoryService,
    IOptions<CartOptions> options) : ICurrentCart
{
    private readonly CartOptions _options = options.Value;

    public async Task<CartResponse> GetAsync(CancellationToken cancellationToken)
    {
        var key = await ResolveReadKeyAsync();
        var items = key is null ? new Dictionary<long, int>() : await store.GetAsync(key);
        return await BuildResponseAsync(key, items, cancellationToken);
    }

    public async Task<CartResponse> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken)
    {
        await ResolveReadKeyAsync();
        var key = keyResolver.GetOrCreateWritableKey();
        var items = await store.GetAsync(key);
        if (!items.ContainsKey(request.SkuId) && items.Count >= _options.MaxLines)
        {
            throw CartErrors.CartFull(_options.MaxLines);
        }

        var quantity = items.GetValueOrDefault(request.SkuId) + request.Quantity;
        await EnsureCanBuyAsync(request.SkuId, quantity, cancellationToken);
        await store.SetQuantityAsync(key, request.SkuId, quantity);
        return await GetAsync(cancellationToken);
    }

    /// <summary>Đặt số lượng; 0 là xóa khỏi giỏ.</summary>
    public async Task<CartResponse> SetQuantityAsync(long skuId, int quantity, CancellationToken cancellationToken)
    {
        await ResolveReadKeyAsync();
        var key = keyResolver.GetOrCreateWritableKey();
        if (quantity == 0)
        {
            await store.RemoveAsync(key, skuId);
        }
        else
        {
            await EnsureCanBuyAsync(skuId, quantity, cancellationToken);
            await store.SetQuantityAsync(key, skuId, quantity);
        }

        return await GetAsync(cancellationToken);
    }

    public Task<CartResponse> RemoveItemAsync(long skuId, CancellationToken cancellationToken) =>
        SetQuantityAsync(skuId, 0, cancellationToken);

    async Task<IReadOnlyList<CartLine>> ICurrentCart.GetLinesAsync(CancellationToken cancellationToken)
    {
        var key = await ResolveReadKeyAsync();
        var items = key is null ? new Dictionary<long, int>() : await store.GetAsync(key);
        return items.Select(item => new CartLine(item.Key, item.Value)).ToList();
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        if (await ResolveReadKeyAsync() is { } key)
        {
            await store.DeleteAsync(key);
        }
    }

    /// <summary>
    /// FR-30: thành viên vừa đăng nhập mà còn giỏ khách (cookie) thì gộp vào giỏ tài khoản,
    /// cộng số lượng nhưng không vượt giới hạn mỗi SKU, rồi xóa giỏ khách.
    /// </summary>
    private async Task<string?> ResolveReadKeyAsync()
    {
        var userKey = keyResolver.UserCartKey;
        var guestKey = keyResolver.GuestCartKey;
        if (userKey is null || guestKey is null)
        {
            return userKey ?? guestKey;
        }

        var guestItems = await store.GetAsync(guestKey);
        if (guestItems.Count > 0)
        {
            var userItems = await store.GetAsync(userKey);
            foreach (var (skuId, quantity) in guestItems)
            {
                var merged = Math.Min(_options.MaxQuantityPerSku, userItems.GetValueOrDefault(skuId) + quantity);
                await store.SetQuantityAsync(userKey, skuId, merged);
            }
        }

        await store.DeleteAsync(guestKey);
        keyResolver.DeleteGuestCookie();
        return userKey;
    }

    private async Task EnsureCanBuyAsync(long skuId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity > _options.MaxQuantityPerSku)
        {
            throw CartErrors.MaxQuantityExceeded(_options.MaxQuantityPerSku);
        }

        var skus = await skuReader.GetSkusAsync([skuId], cancellationToken);
        if (!skus.TryGetValue(skuId, out var sku) || !sku.IsSellable)
        {
            throw CartErrors.SkuNotAvailable();
        }

        var available = await inventoryService.GetAvailableQuantitiesAsync([skuId], cancellationToken);
        if (available.GetValueOrDefault(skuId) < quantity)
        {
            throw CartErrors.OutOfStock();
        }
    }

    private async Task<CartResponse> BuildResponseAsync(
        string? key,
        IReadOnlyDictionary<long, int> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return new CartResponse([], 0, 0);
        }

        var skuIds = items.Keys.ToList();
        var skus = await skuReader.GetSkusAsync(skuIds, cancellationToken);
        var available = await inventoryService.GetAvailableQuantitiesAsync(skuIds, cancellationToken);

        // SKU đã bị xóa khỏi Catalog thì bỏ khỏi giỏ luôn.
        if (key is not null)
        {
            foreach (var missing in skuIds.Where(id => !skus.ContainsKey(id)))
            {
                await store.RemoveAsync(key, missing);
            }
        }

        var lines = items
            .Where(item => skus.ContainsKey(item.Key))
            .Select(item =>
            {
                var sku = skus[item.Key];
                return new CartItemResponse(
                    sku.SkuId,
                    sku.SkuCode,
                    sku.BookTitle,
                    sku.BookSlug,
                    sku.EditionName,
                    sku.CoverType,
                    sku.CoverImageUrl,
                    item.Value,
                    sku.SalePrice,
                    sku.ListPrice,
                    sku.SalePrice * item.Value,
                    sku.IsLatestEdition,
                    sku.IsSellable,
                    available.GetValueOrDefault(sku.SkuId));
            })
            .OrderBy(line => line.BookTitle, StringComparer.Ordinal)
            .ToList();

        return new CartResponse(lines, lines.Sum(line => line.Quantity), lines.Sum(line => line.LineTotal));
    }
}
