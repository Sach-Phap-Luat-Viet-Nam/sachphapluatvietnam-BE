using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using SachLuat.Modules.Cart.Application;
using SachLuat.Modules.Cart.Infrastructure;
using SachLuat.Modules.Catalog.Contracts;
using SachLuat.Modules.Inventory.Contracts;
using SachLuat.SharedKernel.Errors;
using SachLuat.SharedKernel.Security;
using Xunit;

namespace SachLuat.UnitTests.Modules.Cart;

public sealed class CartServiceTests
{
    private const long SkuId = 7;
    private static readonly Guid GuestId = Guid.NewGuid();

    private readonly InMemoryCartStore _store = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ICatalogSkuReader _skuReader = Substitute.For<ICatalogSkuReader>();
    private readonly IInventoryService _inventory = Substitute.For<IInventoryService>();
    private readonly DefaultHttpContext _httpContext = new();
    private readonly CartOptions _options = new();

    public CartServiceTests()
    {
        _skuReader.GetSkusAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, SkuInfo> { [SkuId] = Sku(isSellable: true) });
        SetAvailable(100);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddItem_Guest_CreatesCookieAndReturnsItem()
    {
        var cart = await CreateService().AddItemAsync(new AddCartItemRequest(SkuId, 2), CancellationToken);

        cart.Items.Should().ContainSingle(item => item.SkuId == SkuId && item.Quantity == 2);
        cart.Subtotal.Should().Be(2 * 150_000m);
        _httpContext.Response.Headers.SetCookie.ToString().Should().Contain(CartKeyResolver.CookieName);
    }

    [Fact]
    public async Task AddItem_OverPerSkuLimit_ThrowsMaxQuantity()
    {
        var act = () => CreateService().AddItemAsync(new AddCartItemRequest(SkuId, 21), CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(CartErrors.MaxQuantityExceededCode);
    }

    [Fact]
    public async Task AddItem_NotEnoughStock_ThrowsOutOfStock()
    {
        SetAvailable(1);

        var act = () => CreateService().AddItemAsync(new AddCartItemRequest(SkuId, 2), CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(CartErrors.OutOfStockCode);
    }

    [Fact]
    public async Task AddItem_NotSellable_ThrowsSkuNotAvailable()
    {
        _skuReader.GetSkusAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, SkuInfo> { [SkuId] = Sku(isSellable: false) });

        var act = () => CreateService().AddItemAsync(new AddCartItemRequest(SkuId, 1), CancellationToken);

        (await act.Should().ThrowAsync<BusinessException>()).Which.Code.Should().Be(CartErrors.SkuNotAvailableCode);
    }

    [Fact]
    public async Task Get_MemberWithGuestCart_MergesCappedAtLimitAndDeletesGuestCart()
    {
        var guestKey = _options.KeyPrefix + "g:" + GuestId.ToString("N");
        var userKey = _options.KeyPrefix + "u:5";
        await _store.SetQuantityAsync(guestKey, SkuId, 15);
        await _store.SetQuantityAsync(userKey, SkuId, 10);
        _httpContext.Request.Headers.Cookie = $"{CartKeyResolver.CookieName}={GuestId:N}";
        _currentUser.UserId.Returns(5);

        var cart = await CreateService().GetAsync(CancellationToken);

        cart.Items.Single().Quantity.Should().Be(_options.MaxQuantityPerSku);
        (await _store.GetAsync(guestKey)).Should().BeEmpty();
    }

    private CartService CreateService()
    {
        var accessor = new HttpContextAccessor { HttpContext = _httpContext };
        var options = Options.Create(_options);
        return new CartService(
            _store, new CartKeyResolver(accessor, _currentUser, options), _skuReader, _inventory, options);
    }

    private void SetAvailable(int quantity) =>
        _inventory.GetAvailableQuantitiesAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<long, int> { [SkuId] = quantity });

    private static SkuInfo Sku(bool isSellable) => new(
        SkuId, "SLVN-BLDS-2026-BM", 1, "Bộ luật Dân sự", "bo-luat-dan-su", 1, null, "9780306406157", "paperback",
        200_000m, 150_000m, 150_000m, 500, null, IsLatestEdition: true, CategoryIds: [], IsSellable: isSellable);

    private sealed class InMemoryCartStore : ICartStore
    {
        private readonly Dictionary<string, Dictionary<long, int>> _carts = [];

        public Task<IReadOnlyDictionary<long, int>> GetAsync(string cartKey) =>
            Task.FromResult<IReadOnlyDictionary<long, int>>(
                _carts.TryGetValue(cartKey, out var items) ? new Dictionary<long, int>(items) : []);

        public Task SetQuantityAsync(string cartKey, long skuId, int quantity)
        {
            if (!_carts.TryGetValue(cartKey, out var items))
            {
                _carts[cartKey] = items = [];
            }

            items[skuId] = quantity;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string cartKey, long skuId)
        {
            _carts.GetValueOrDefault(cartKey)?.Remove(skuId);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string cartKey)
        {
            _carts.Remove(cartKey);
            return Task.CompletedTask;
        }
    }
}
