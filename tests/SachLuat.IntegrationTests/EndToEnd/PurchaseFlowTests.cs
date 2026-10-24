using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SachLuat.Modules.Ordering.Application;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

/// <summary>
/// Luồng nghiệp vụ chính chạy trên CSDL + Redis thật: Admin bật 2FA → nhập Catalog → phiếu nhập kho được duyệt
/// → khách mua hàng → đóng gói trừ kho FIFO → hủy đơn hoàn kho → tự hủy đơn quá hạn thanh toán.
/// </summary>
[Collection(EndToEndCollection.Name)]
public sealed class PurchaseFlowTests(EndToEndFactory factory)
{
    private const string CustomerPhone = "0901234567";

    [Fact]
    public async Task FullPurchaseFlow()
    {
        factory.SkipIfNotConfigured();

        // 1. Super Admin đầu tiên (tạo từ cấu hình) phải bật 2FA mới dùng được API Admin.
        using var admin = new ApiClient(factory);
        var adminSession = await admin.LoginAsync(
            EndToEndFactory.AdminEmail, EndToEndFactory.AdminPassword, portal: "admin");
        adminSession.GetProperty("user").GetProperty("requiresTwoFactorSetup").GetBoolean().Should().BeTrue();
        await admin.GetAsync("/api/v1/admin/books", HttpStatusCode.Forbidden);
        await admin.EnableTwoFactorAsync();

        // 2. Tạo thủ kho; thủ kho cũng phải bật 2FA.
        var keeperId = (await admin.PostAsync("/api/v1/admin/users", new
        {
            email = "kho@e2e.test",
            fullName = "Thủ kho",
            password = "kho-password-123",
            roles = new[] { "warehouse_keeper" },
        }, HttpStatusCode.Created)).GetProperty("id").GetInt64();
        using var keeper = new ApiClient(factory);
        var keeperSession = await keeper.LoginAsync("kho@e2e.test", "kho-password-123", portal: "admin");
        keeperSession.GetProperty("loginVia").GetString().Should().Be("portal");
        await keeper.EnableTwoFactorAsync();

        // 2b. Fast login: Super Admin vào hộ thủ kho (chỉ access token, cờ loginVia=admin); không vào được chính mình.
        var adminId = adminSession.GetProperty("user").GetProperty("id").GetInt64();
        var impersonated = await admin.PostAsync($"/api/v1/admin/users/{keeperId}/impersonate");
        impersonated.GetProperty("loginVia").GetString().Should().Be("admin");
        impersonated.GetProperty("impersonatorId").GetInt64().Should().Be(adminId);
        using var asKeeper = new ApiClient(factory)
        {
            AccessToken = impersonated.GetProperty("accessToken").GetString(),
        };
        (await asKeeper.GetAsync("/api/v1/me")).GetProperty("email").GetString().Should().Be("kho@e2e.test");
        var selfImpersonation = await admin.PostAsync(
            $"/api/v1/admin/users/{adminId}/impersonate", expected: HttpStatusCode.UnprocessableEntity);
        selfImpersonation.GetProperty("code").GetString().Should().Be("CANNOT_IMPERSONATE");

        // 2c. Đăng xuất hộ: Super Admin thu hồi mọi phiên của một nhân viên; nhân viên không refresh được nữa.
        var leaverId = (await admin.PostAsync("/api/v1/admin/users", new
        {
            email = "nghiviec@e2e.test",
            fullName = "Nhân viên nghỉ việc",
            password = "leaver-password-123",
            roles = new[] { "customer_service" },
        }, HttpStatusCode.Created)).GetProperty("id").GetInt64();
        using var leaver = new ApiClient(factory);
        await leaver.LoginAsync("nghiviec@e2e.test", "leaver-password-123", portal: "admin");
        (await admin.PostAsync($"/api/v1/admin/users/{leaverId}/logout"))
            .GetProperty("revokedSessions").GetInt32().Should().Be(1);
        (await leaver.PostAsync("/api/v1/auth/refresh", expected: HttpStatusCode.Unauthorized))
            .GetProperty("code").GetString().Should().Be("INVALID_REFRESH_TOKEN");
        (await admin.PostAsync("/api/v1/admin/users/999999/logout", expected: HttpStatusCode.NotFound))
            .GetProperty("code").GetString().Should().Be("USER_NOT_FOUND");

        // 2d. Super Admin đặt mật khẩu mới: mở khóa tài khoản bị khóa do sai mật khẩu, thu hồi phiên cũ.
        var forgetfulId = (await admin.PostAsync("/api/v1/admin/users", new
        {
            email = "quenmk@e2e.test",
            fullName = "Nhân viên quên mật khẩu",
            password = "old-password-123",
            roles = new[] { "customer_service" },
        }, HttpStatusCode.Created)).GetProperty("id").GetInt64();
        using var forgetful = new ApiClient(factory);
        await forgetful.LoginAsync("quenmk@e2e.test", "old-password-123");
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await forgetful.PostAsync("/api/v1/auth/login",
                new { email = "quenmk@e2e.test", password = "sai-mat-khau" },
                attempt < 5 ? HttpStatusCode.Unauthorized : HttpStatusCode.Locked);
        }

        (await admin.PutAsync($"/api/v1/admin/users/{forgetfulId}/password", new { newPassword = "ngan" },
            HttpStatusCode.BadRequest)).GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        await admin.PutAsync($"/api/v1/admin/users/{forgetfulId}/password", new { newPassword = "new-password-123" },
            HttpStatusCode.NoContent);
        (await forgetful.PostAsync("/api/v1/auth/refresh", expected: HttpStatusCode.Unauthorized))
            .GetProperty("code").GetString().Should().Be("INVALID_REFRESH_TOKEN");
        await forgetful.PostAsync("/api/v1/auth/login",
            new { email = "quenmk@e2e.test", password = "old-password-123" }, HttpStatusCode.Unauthorized);
        await forgetful.LoginAsync("quenmk@e2e.test", "new-password-123");
        (await admin.PutAsync($"/api/v1/admin/users/{adminId}/password", new { newPassword = "new-password-123" },
            HttpStatusCode.UnprocessableEntity)).GetProperty("code").GetString().Should().Be("CANNOT_MANAGE_USER");
        (await admin.PutAsync("/api/v1/admin/users/999999/password", new { newPassword = "new-password-123" },
            HttpStatusCode.NotFound)).GetProperty("code").GetString().Should().Be("USER_NOT_FOUND");

        // 2e. Vô hiệu hóa: thu hồi phiên, chặn đăng nhập (sai mật khẩu vẫn chỉ báo sai) và fast login; kích hoạt lại.
        (await admin.PostAsync($"/api/v1/admin/users/{forgetfulId}/deactivate"))
            .GetProperty("isActive").GetBoolean().Should().BeFalse();
        (await forgetful.PostAsync("/api/v1/auth/refresh", expected: HttpStatusCode.Unauthorized))
            .GetProperty("code").GetString().Should().Be("INVALID_REFRESH_TOKEN");
        (await forgetful.PostAsync("/api/v1/auth/login",
                new { email = "quenmk@e2e.test", password = "new-password-123" }, HttpStatusCode.Forbidden))
            .GetProperty("code").GetString().Should().Be("ACCOUNT_DISABLED");
        (await forgetful.PostAsync("/api/v1/auth/login",
                new { email = "quenmk@e2e.test", password = "sai-mat-khau" }, HttpStatusCode.Unauthorized))
            .GetProperty("code").GetString().Should().Be("INVALID_CREDENTIALS");
        (await admin.PostAsync($"/api/v1/admin/users/{forgetfulId}/impersonate", expected: HttpStatusCode.Forbidden))
            .GetProperty("code").GetString().Should().Be("ACCOUNT_DISABLED");
        (await admin.PostAsync(
                $"/api/v1/admin/users/{adminId}/deactivate", expected: HttpStatusCode.UnprocessableEntity))
            .GetProperty("code").GetString().Should().Be("CANNOT_MANAGE_USER");
        (await admin.PostAsync($"/api/v1/admin/users/{forgetfulId}/activate"))
            .GetProperty("isActive").GetBoolean().Should().BeTrue();
        (await forgetful.LoginAsync("quenmk@e2e.test", "new-password-123"))
            .GetProperty("user").GetProperty("isActive").GetBoolean().Should().BeTrue();

        // 3. Catalog: danh mục, tác giả, văn bản, đầu sách, ấn bản đang bán, SKU.
        var categoryId = (await admin.PostAsync("/api/v1/admin/categories",
            new { name = "Dân sự", slug = "dan-su", sortOrder = 1 }, HttpStatusCode.Created)).GetProperty("id").GetInt64();
        var authorId = (await admin.PostAsync("/api/v1/admin/authors",
            new { name = "Ban biên tập", slug = "ban-bien-tap", sortOrder = 1 }, HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var documentId = (await admin.PostAsync("/api/v1/admin/legal-documents",
            new { docNumber = "91/2015/QH13", docType = "code", title = "Bộ luật Dân sự" }, HttpStatusCode.Created))
            .GetProperty("id").GetInt64();
        var book = await admin.PostAsync("/api/v1/admin/books", new
        {
            title = "Bộ luật Dân sự 2026",
            slug = "bo-luat-dan-su-2026",
            bookType = "code",
            status = "active",
            targetAudiences = new[] { "lawyer", "student" },
            categoryIds = new[] { categoryId },
            authorIds = new[] { authorId },
        }, HttpStatusCode.Created);
        var bookId = book.GetProperty("id").GetInt64();
        book = await admin.PostAsync($"/api/v1/admin/books/{bookId}/editions", new
        {
            isbn = "978-0-306-40615-7",
            publisher = "NXB Tư pháp",
            publishingDecisionNo = "123/QĐ-NXBTP",
            status = "on_sale",
            editionName = "Tái bản lần 3 – 2026",
            updatedToText = "Cập nhật đến Bộ luật Dân sự 91/2015/QH13",
            weightGram = 600,
            legalDocumentIds = new[] { documentId },
        }, HttpStatusCode.Created);
        var editionId = book.GetProperty("editions")[0].GetProperty("id").GetInt64();
        book.GetProperty("editions")[0].GetProperty("isLatest").GetBoolean().Should().BeTrue();
        book = await admin.PostAsync($"/api/v1/admin/editions/{editionId}/skus", new
        {
            skuCode = "SLVN-BLDS-2026-BM",
            coverType = "paperback",
            listPrice = 200_000,
            salePrice = 150_000,
            allowPreorder = false,
        }, HttpStatusCode.Created);
        var skuId = book.GetProperty("editions")[0].GetProperty("skus")[0].GetProperty("id").GetInt64();

        // Trùng slug → 409 thay vì 500.
        (await admin.PostAsync("/api/v1/admin/books",
            new { title = "Trùng", slug = "bo-luat-dan-su-2026", bookType = "code", status = "draft" },
            HttpStatusCode.Conflict)).GetProperty("code").GetString().Should().Be("SLUG_DUPLICATE");

        // 4. Phiếu nhập 2 lô: thủ kho lập, không tự duyệt được; Super Admin duyệt thì tồn tăng.
        var receipt = await keeper.PostAsync("/api/v1/admin/inventory/receipts", new
        {
            note = "Nhập lô đầu",
            lines = new object[]
            {
                new { skuId, batchCode = "LO-01", quantity = 3, unitCost = 50_000, receivedAt = "2026-10-01" },
                new { skuId, batchCode = "LO-02", quantity = 10, unitCost = 60_000, receivedAt = "2026-10-10" },
            },
        }, HttpStatusCode.Created);
        var receiptId = receipt.GetProperty("id").GetInt64();
        await keeper.PostAsync($"/api/v1/admin/inventory/receipts/{receiptId}/approve", expected: HttpStatusCode.Forbidden);
        (await admin.PostAsync($"/api/v1/admin/inventory/receipts/{receiptId}/approve"))
            .GetProperty("status").GetString().Should().Be("approved");
        (await StockAsync(keeper, skuId)).GetProperty("available").GetInt32().Should().Be(13);

        // 5. Website: danh sách, lọc theo số hiệu văn bản, chi tiết, cây danh mục.
        using var guest = new ApiClient(factory);
        var list = await guest.GetAsync("/api/v1/books?legalDocument=91/2015/QH13&audience=lawyer");
        list.GetProperty("meta").GetProperty("total").GetInt32().Should().Be(1);
        var summary = list.GetProperty("items")[0];
        summary.GetProperty("inStock").GetBoolean().Should().BeTrue();
        summary.GetProperty("salePrice").GetDecimal().Should().Be(150_000m);
        summary.GetProperty("discountPercent").GetInt32().Should().Be(25);
        var detail = await guest.GetAsync("/api/v1/books/bo-luat-dan-su-2026");
        detail.GetProperty("editions")[0].GetProperty("legalDocuments")[0].GetProperty("docNumber").GetString()
            .Should().Be("91/2015/QH13");
        (await guest.GetAsync("/api/v1/categories"))[0].GetProperty("slug").GetString().Should().Be("dan-su");

        // 6. Khách vãng lai: giỏ hàng (cookie), giới hạn 20 cuốn/SKU, đặt COD có Idempotency-Key.
        (await guest.PostAsync("/api/v1/cart/items", new { skuId, quantity = 5 }))
            .GetProperty("totalQuantity").GetInt32().Should().Be(5);
        (await guest.PostAsync("/api/v1/cart/items", new { skuId, quantity = 30 }, (HttpStatusCode)422))
            .GetProperty("code").GetString().Should().Be("MAX_QTY_EXCEEDED");
        var idempotencyKey = Guid.NewGuid().ToString();
        var order = await guest.PostAsync("/api/v1/orders", NewOrder("cod"), HttpStatusCode.Created, idempotencyKey);
        var orderCode = order.GetProperty("orderCode").GetString()!;
        order.GetProperty("status").GetString().Should().Be("pending_confirm");
        order.GetProperty("shippingFee").GetDecimal().Should().Be(0m, "đơn 750.000đ ≥ 300.000đ được miễn ship (BR-14)");
        order.GetProperty("total").GetDecimal().Should().Be(750_000m);
        (await guest.PostAsync("/api/v1/orders", NewOrder("cod"), HttpStatusCode.Created, idempotencyKey))
            .GetProperty("orderCode").GetString().Should().Be(orderCode, "bấm đặt hàng lần hai không tạo đơn mới");
        (await guest.GetAsync("/api/v1/cart")).GetProperty("totalQuantity").GetInt32().Should().Be(0);
        var stock = await StockAsync(keeper, skuId);
        stock.GetProperty("available").GetInt32().Should().Be(8);
        stock.GetProperty("reserved").GetInt32().Should().Be(5);

        // 7. Đóng gói: trừ tồn thật theo FIFO (3 cuốn lô 01 + 2 cuốn lô 02), ghi giá vốn.
        await admin.PostAsync($"/api/v1/admin/orders/{orderCode}/status", new { status = "confirmed" });
        var packed = await admin.PostAsync($"/api/v1/admin/orders/{orderCode}/status", new { status = "packing" });
        packed.GetProperty("costOfGoods").GetDecimal().Should().Be(3 * 50_000m + 2 * 60_000m);
        stock = await StockAsync(keeper, skuId);
        stock.GetProperty("reserved").GetInt32().Should().Be(0);
        stock.GetProperty("batches")[0].GetProperty("remaining").GetInt32().Should().Be(0);
        stock.GetProperty("batches")[1].GetProperty("remaining").GetInt32().Should().Be(8);
        (await admin.PostAsync($"/api/v1/admin/orders/{orderCode}/status", new { status = "delivered" },
            HttpStatusCode.Conflict)).GetProperty("code").GetString().Should().Be("ORDER_INVALID_TRANSITION");

        // 8. Tra cứu đơn không cần đăng nhập (mã đơn + SĐT).
        (await guest.PostAsync("/api/v1/orders/lookup", new { orderCode, phone = CustomerPhone }))
            .GetProperty("status").GetString().Should().Be("packing");

        // 9. Thành viên: đặt chuyển khoản rồi tự hủy (BR-12) → hoàn tồn tạm giữ.
        using var member = new ApiClient(factory);
        var registered = await member.PostAsync("/api/v1/auth/register", new
        {
            email = "khach@e2e.test",
            password = "khach-password",
            fullName = "Khách thành viên",
        }, HttpStatusCode.Created);
        member.AccessToken = registered.GetProperty("accessToken").GetString();
        await member.PostAsync("/api/v1/cart/items", new { skuId, quantity = 2 });
        var memberOrder = await member.PostAsync("/api/v1/orders", NewOrder("bank_transfer"), HttpStatusCode.Created);
        memberOrder.GetProperty("status").GetString().Should().Be("pending_payment");
        memberOrder.GetProperty("shippingFee").GetDecimal().Should().Be(0m);
        var memberCode = memberOrder.GetProperty("orderCode").GetString();
        (await member.GetAsync("/api/v1/orders")).GetProperty("meta").GetProperty("total").GetInt32().Should().Be(1);
        (await member.PostAsync($"/api/v1/orders/{memberCode}/cancel", new { reason = "Đặt nhầm" }))
            .GetProperty("status").GetString().Should().Be("cancelled");
        (await StockAsync(keeper, skuId)).GetProperty("available").GetInt32().Should().Be(8);

        // 10. Đơn chuyển khoản quá 30 phút chưa thanh toán thì job tự hủy và hoàn tồn.
        await member.PostAsync("/api/v1/cart/items", new { skuId, quantity = 1 });
        var expiring = await member.PostAsync("/api/v1/orders", NewOrder("bank_transfer"), HttpStatusCode.Created);
        (await StockAsync(keeper, skuId)).GetProperty("available").GetInt32().Should().Be(7);
        factory.Clock.Advance(TimeSpan.FromMinutes(31));
        var sweeper = factory.Services.GetServices<IHostedService>().OfType<ExpiredPaymentCancellationService>().Single();
        (await sweeper.SweepAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await guest.PostAsync("/api/v1/orders/lookup",
                new { orderCode = expiring.GetProperty("orderCode").GetString(), phone = CustomerPhone }))
            .GetProperty("status").GetString().Should().Be("cancelled");
    }

    private static Task<JsonElement> StockAsync(ApiClient client, long skuId) =>
        client.GetAsync($"/api/v1/admin/inventory/skus/{skuId}");

    private static object NewOrder(string paymentMethod) => new
    {
        customerName = "Nguyễn Văn A",
        phone = CustomerPhone,
        shippingAddress = new { province = "Hà Nội", ward = "Phường Cầu Giấy", streetAddress = "1 Xuân Thủy" },
        paymentMethod,
    };
}
