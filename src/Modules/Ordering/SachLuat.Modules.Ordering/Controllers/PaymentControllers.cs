using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SachLuat.Modules.Ordering.Application;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Ordering.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/payments/sepay")]
internal sealed class SepayWebhookController(SepayPaymentService service) : ControllerBase
{
    /// <summary>
    /// Webhook SePay báo tiền vào tài khoản. Xác thực bằng header <c>Authorization: Apikey ...</c>; trả 200
    /// <c>{"success": true}</c> khi đã nhận (kể cả giao dịch không khớp đơn, vì đã ghi lại để Admin đối soát).
    /// </summary>
    [HttpPost("webhook")]
    public async Task<SepayWebhookResponse> Webhook(
        SepayWebhookPayload payload,
        [FromHeader(Name = "Authorization")] string? authorization,
        CancellationToken cancellationToken)
    {
        service.EnsureAuthorized(authorization);
        await service.HandleWebhookAsync(payload, cancellationToken);
        return new SepayWebhookResponse(true);
    }
}

[ApiController]
[Route(ApiRoutes.AdminV1 + "/payments/sepay-transactions")]
internal sealed class AdminSepayTransactionsController(SepayPaymentService service) : ControllerBase
{
    /// <summary>Giao dịch SePay đã nhận, lọc theo kết quả để đối soát các giao dịch không tự khớp được.</summary>
    [Authorize(Policy = Policies.OrdersRead)]
    [HttpGet]
    public Task<PagedResponse<SepayTransactionResponse>> List(
        [FromQuery] AdminSepayTransactionsQuery query,
        CancellationToken cancellationToken) => service.ListTransactionsAsync(query, cancellationToken);
}
