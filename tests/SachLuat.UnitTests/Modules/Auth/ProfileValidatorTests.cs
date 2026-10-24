using AwesomeAssertions;
using SachLuat.Modules.Auth.Application;
using Xunit;

namespace SachLuat.UnitTests.Modules.Auth;

public sealed class ProfileValidatorTests
{
    [Fact]
    public void Address_Valid_IsValid()
    {
        var result = new UpsertAddressRequestValidator().Validate(Address(phone: "+84901234567"));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("0901 234 567")]
    [InlineData("")]
    public void Address_InvalidPhone_IsInvalid(string phone)
    {
        var result = new UpsertAddressRequestValidator().Validate(Address(phone: phone));

        result.Errors.Should().Contain(error => error.PropertyName == "Phone");
    }

    [Fact]
    public void Address_MissingRequiredFields_ReportsEachField()
    {
        var result = new UpsertAddressRequestValidator()
            .Validate(new UpsertAddressRequest("", "0901234567", "", "", "", IsDefault: false));

        result.Errors.Select(error => error.PropertyName).Should()
            .Contain(["RecipientName", "Province", "Ward", "StreetAddress"]);
    }

    [Theory]
    [InlineData("0101234567")]
    [InlineData("0101234567-001")]
    public void InvoiceInfo_ValidTaxCode_IsValid(string taxCode)
    {
        var result = new UpsertInvoiceInfoRequestValidator().Validate(InvoiceInfo(taxCode));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("0101234567-1")]
    [InlineData("MST0101234567")]
    public void InvoiceInfo_InvalidTaxCode_IsInvalid(string taxCode)
    {
        var result = new UpsertInvoiceInfoRequestValidator().Validate(InvoiceInfo(taxCode));

        result.Errors.Should().ContainSingle(error => error.PropertyName == "TaxCode");
    }

    private static UpsertAddressRequest Address(string phone) =>
        new("Nguyễn Văn A", phone, "Hà Nội", "Phường Cửa Nam", "12 Hàng Bài", IsDefault: false);

    private static UpsertInvoiceInfoRequest InvoiceInfo(string taxCode) =>
        new("Công ty TNHH Luật ABC", taxCode, "1 Tràng Tiền, Hà Nội", "ketoan@abc.vn");
}
