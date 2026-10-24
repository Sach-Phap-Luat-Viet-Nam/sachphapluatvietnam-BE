using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

/// <summary>Sổ địa chỉ và thông tin hóa đơn trả về trong <c>/me</c> để điền sẵn form đặt hàng.</summary>
[Collection(EndToEndCollection.Name)]
public sealed class ProfileTests(EndToEndFactory factory)
{
    [Fact]
    public async Task AddressBookAndInvoiceInfo_PrefillMe()
    {
        factory.SkipIfNotConfigured();
        using var client = new ApiClient(factory);
        var session = await client.PostAsync("/api/v1/auth/register", new
        {
            email = "profile@e2e.test",
            password = "profile-password",
            fullName = "Hồ sơ khách",
        }, HttpStatusCode.Created);
        client.AccessToken = session.GetProperty("accessToken").GetString();

        var empty = await client.GetAsync("/api/v1/me");
        empty.GetProperty("defaultAddress").ValueKind.Should().Be(JsonValueKind.Null);
        empty.GetProperty("invoiceInfo").ValueKind.Should().Be(JsonValueKind.Null);

        // Địa chỉ đầu tiên tự là mặc định; địa chỉ thứ hai đặt làm mặc định thì đứng đầu danh sách.
        await client.PostAsync("/api/v1/me/addresses", Address("Nhà riêng", isDefault: false), HttpStatusCode.Created);
        var addresses = await client.PostAsync(
            "/api/v1/me/addresses", Address("Văn phòng", isDefault: true), HttpStatusCode.Created);
        addresses.GetArrayLength().Should().Be(2);
        addresses[0].GetProperty("recipientName").GetString().Should().Be("Văn phòng");
        addresses[0].GetProperty("isDefault").GetBoolean().Should().BeTrue();
        addresses[1].GetProperty("isDefault").GetBoolean().Should().BeFalse();

        await client.PutAsync("/api/v1/me/invoice-info", new
        {
            companyName = "Công ty TNHH Luật ABC",
            taxCode = "0101234567",
            address = "1 Tràng Tiền, Hà Nội",
            email = "ketoan@abc.vn",
        });

        var me = await client.GetAsync("/api/v1/me");
        me.GetProperty("defaultAddress").GetProperty("recipientName").GetString().Should().Be("Văn phòng");
        me.GetProperty("invoiceInfo").GetProperty("taxCode").GetString().Should().Be("0101234567");

        // Xóa địa chỉ mặc định: địa chỉ còn lại thành mặc định.
        var officeId = addresses[0].GetProperty("id").GetInt64();
        await client.SendAsync(HttpMethod.Delete, $"/api/v1/me/addresses/{officeId}", null, HttpStatusCode.NoContent);
        var remaining = await client.GetAsync("/api/v1/me/addresses");
        remaining.GetArrayLength().Should().Be(1);
        remaining[0].GetProperty("isDefault").GetBoolean().Should().BeTrue();

        (await client.SendAsync(
                HttpMethod.Delete, $"/api/v1/me/addresses/{officeId}", null, HttpStatusCode.NotFound))
            .GetProperty("code").GetString().Should().Be("ADDRESS_NOT_FOUND");
    }

    private static object Address(string recipientName, bool isDefault) => new
    {
        recipientName,
        phone = "0901234567",
        province = "Hà Nội",
        ward = "Phường Cửa Nam",
        streetAddress = "12 Hàng Bài",
        isDefault,
    };
}
