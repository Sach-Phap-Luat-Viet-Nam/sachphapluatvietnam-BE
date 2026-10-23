using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Inventory.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Inventory.Controllers;

[ApiController]
[Authorize(Policy = Policies.InventoryAdmin)]
[Route(ApiRoutes.AdminV1 + "/inventory/receipts")]
internal sealed class AdminReceiptsController(InventoryAdminService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<ReceiptResponse>> List([FromQuery] ReceiptsQuery query, CancellationToken cancellationToken) =>
        service.ListReceiptsAsync(query, cancellationToken);

    [HttpGet("{id:long}")]
    public Task<ReceiptResponse> Get(long id, CancellationToken cancellationToken) =>
        service.GetReceiptAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ReceiptResponse>> Create(
        CreateReceiptRequest request,
        CancellationToken cancellationToken) => StatusCode(
            StatusCodes.Status201Created,
            await service.CreateReceiptAsync(currentUser.RequireUserId(), request, cancellationToken));

    [Authorize(Policy = Policies.InventoryApprove)]
    [HttpPost("{id:long}/approve")]
    public Task<ReceiptResponse> Approve(long id, CancellationToken cancellationToken) =>
        service.ApproveReceiptAsync(currentUser.RequireUserId(), id, cancellationToken);

    [Authorize(Policy = Policies.InventoryApprove)]
    [HttpPost("{id:long}/reject")]
    public Task<ReceiptResponse> Reject(long id, RejectReceiptRequest request, CancellationToken cancellationToken) =>
        service.RejectReceiptAsync(currentUser.RequireUserId(), id, request, cancellationToken);
}

[ApiController]
[Authorize(Policy = Policies.InventoryAdmin)]
[Route(ApiRoutes.AdminV1 + "/inventory")]
internal sealed class AdminStockController(InventoryAdminService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("skus/{skuId:long}")]
    public Task<StockCardResponse> StockCard(long skuId, CancellationToken cancellationToken) =>
        service.GetStockCardAsync(skuId, cancellationToken);

    [HttpPut("skus/{skuId:long}/threshold")]
    public Task<StockCardResponse> SetThreshold(
        long skuId,
        SetThresholdRequest request,
        CancellationToken cancellationToken) => service.SetThresholdAsync(skuId, request, cancellationToken);

    [HttpGet("low-stock")]
    public Task<PagedResponse<LowStockResponse>> LowStock(
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken) => service.ListLowStockAsync(query, cancellationToken);

    [HttpPost("issues")]
    public Task<StockCardResponse> Issue(IssueStockRequest request, CancellationToken cancellationToken) =>
        service.IssueStockAsync(currentUser.RequireUserId(), request, cancellationToken);
}
