using AwesomeAssertions;
using Microsoft.Extensions.Options;
using SachLuat.Modules.Catalog.Application.Admin;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.SharedKernel.Media;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class QuickCreateBookRequestValidatorTests
{
    private static readonly QuickCreateBookRequestValidator Validator = new(
        new UpsertBookRequestValidator(),
        new UpsertEditionRequestValidator(Options.Create(new CloudinaryOptions())),
        new CreateSkuRequestValidator());

    [Fact]
    public void BookWithOneSku_IsValid()
    {
        var result = Validator.Validate(Request(Sku("PL250-BM", CoverType.Paperback)));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void BookWithoutSku_IsInvalid()
    {
        var result = Validator.Validate(Request());

        result.Errors.Should().ContainSingle(error => error.PropertyName == "Skus");
    }

    [Fact]
    public void TwoSkusOfTheSameCoverType_AreInvalid()
    {
        var result = Validator.Validate(Request(Sku("PL250-A", CoverType.Paperback), Sku("PL250-B", CoverType.Paperback)));

        result.Errors.Should().Contain(error => error.ErrorMessage == "Mỗi loại bìa chỉ có một SKU");
    }

    [Fact]
    public void TwoSkusWithTheSameCodeIgnoringCase_AreInvalid()
    {
        var result = Validator.Validate(Request(Sku("pl250", CoverType.Paperback), Sku("PL250", CoverType.Hardcover)));

        result.Errors.Should().Contain(error => error.ErrorMessage == "Mã SKU bị trùng nhau");
    }

    [Fact]
    public void InvalidPartsReportTheirOwnFields()
    {
        var request = Request(Sku("PL250", CoverType.Paperback, listPrice: 100_000m, salePrice: 150_000m)) with
        {
            Edition = Edition() with { Isbn = "123" },
        };

        var result = Validator.Validate(request);

        result.Errors.Select(error => error.PropertyName).Should()
            .Contain(["Edition.Isbn", "Skus[0].SalePrice"]);
    }

    private static QuickCreateBookRequest Request(params CreateSkuRequest[] skus) => new(
        new UpsertBookRequest(
            "Luật Quản lý nợ công", "luat-quan-ly-no-cong", BookType.Code, BookStatus.Active,
            null, null, null, null, null, null),
        Edition(),
        skus);

    private static UpsertEditionRequest Edition() => new(
        "9786040000002", "NXB Tài Chính", "123/QĐ-NXBTC", EditionStatus.OnSale, null, null, 2026,
        null, null, 400, null, 800, null, null, null, null, null);

    private static CreateSkuRequest Sku(
        string code,
        CoverType coverType,
        decimal listPrice = 250_000m,
        decimal salePrice = 199_000m) => new(code, coverType, listPrice, salePrice, false);
}
