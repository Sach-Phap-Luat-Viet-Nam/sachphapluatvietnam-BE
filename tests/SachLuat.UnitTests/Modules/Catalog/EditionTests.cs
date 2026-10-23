using AwesomeAssertions;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class EditionTests
{
    [Fact]
    public void AddSku_NormalizesCodeAndKeepsPrices()
    {
        var edition = CreateEdition();

        var sku = edition.AddSku(" slvn-blds-2026-bm ", CoverType.Paperback, 250_000m, 199_000m);

        sku.SkuCode.Should().Be("SLVN-BLDS-2026-BM");
        sku.ListPrice.Should().Be(250_000m);
        sku.SalePrice.Should().Be(199_000m);
    }

    [Fact]
    public void AddSku_SameCoverTypeTwice_ThrowsDuplicate()
    {
        var edition = CreateEdition();
        edition.AddSku("SLVN-BLDS-2026-BM", CoverType.Paperback, 250_000m, 250_000m);

        var act = () => edition.AddSku("SLVN-BLDS-2026-BM2", CoverType.Paperback, 250_000m, 250_000m);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.SkuCoverDuplicateCode);
    }

    [Theory]
    [InlineData(250_000, 260_000)] // giá bán vượt giá bìa
    [InlineData(0, 0)]
    [InlineData(250_000, -1)]
    [InlineData(250_000.5, 199_000)] // VNĐ không có phần lẻ
    public void AddSku_InvalidPrices_ThrowsPriceInvalid(double listPrice, double salePrice)
    {
        var edition = CreateEdition();

        var act = () => edition.AddSku("SLVN-BLDS-2026-BM", CoverType.Paperback, (decimal)listPrice, (decimal)salePrice);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.PriceInvalidCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SLVN BLDS")]
    [InlineData("SLVN_BLDS")]
    public void AddSku_InvalidCode_ThrowsSkuCodeInvalid(string skuCode)
    {
        var edition = CreateEdition();

        var act = () => edition.AddSku(skuCode, CoverType.Paperback, 250_000m, 250_000m);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.SkuCodeInvalidCode);
    }

    [Fact]
    public void UpdateDetails_NonPositivePages_ThrowsValidationError()
    {
        var edition = CreateEdition();

        var act = () => edition.UpdateDetails(new EditionDetails(Pages: 0));

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(ErrorCodes.ValidationError);
    }

    private static Edition CreateEdition() =>
        new Book("Bộ luật Dân sự", "bo-luat-dan-su", BookType.Code)
            .AddEdition(new EditionPublishingInfo("9780306406157", "NXB Tư pháp", "123/QĐ-NXBTP"));
}
