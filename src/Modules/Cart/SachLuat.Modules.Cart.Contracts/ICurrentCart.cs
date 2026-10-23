namespace SachLuat.Modules.Cart.Contracts;

/// <summary>Giỏ hàng của request hiện tại (thành viên theo tài khoản, khách vãng lai theo cookie).</summary>
public interface ICurrentCart
{
    Task<IReadOnlyList<CartLine>> GetLinesAsync(CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}

public sealed record CartLine(long SkuId, int Quantity);
