using SachLuat.Modules.Inventory.Contracts;

namespace SachLuat.IntegrationTests.Catalog;

/// <summary>Kho rỗng cho test truy vấn Catalog; các thao tác ghi kho không được gọi tới.</summary>
internal sealed class NoStock : IInventoryService
{
    public Task<IReadOnlyDictionary<long, int>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<long> skuIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<long, int>>(new Dictionary<long, int>());

    public Task ReserveAsync(
        string reference,
        IReadOnlyCollection<StockLine> lines,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ReleaseAsync(string reference, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyDictionary<long, decimal>> DeductAsync(
        string reference,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task RestockAsync(string reference, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
