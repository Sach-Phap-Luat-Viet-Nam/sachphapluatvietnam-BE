using AwesomeAssertions;
using SachLuat.Modules.Shipping.Application;
using SachLuat.Modules.Shipping.Contracts;
using SachLuat.Modules.Shipping.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Shipping;

public sealed class ProvinceKeyTests
{
    [Theory]
    [InlineData("Hà Nội", "ha noi")]
    [InlineData("Thành phố Hà Nội", "ha noi")]
    [InlineData("TP. Hà Nội", "ha noi")]
    [InlineData("  tp hà   nội ", "ha noi")]
    [InlineData("Tỉnh Thanh Hóa", "thanh hoa")]
    [InlineData("Thanh Hóa", "thanh hoa")] // "Thanh" không phải tiền tố "Thành phố"
    [InlineData("Đà Nẵng", "da nang")]
    [InlineData("Thành phố Hồ Chí Minh", "ho chi minh")]
    [InlineData("Bà Rịa - Vũng Tàu", "ba ria vung tau")]
    public void Normalize_IgnoresCaseDiacriticsPunctuationAndAdministrativePrefix(string input, string expected)
    {
        ProvinceKey.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyInput_IsEmpty(string? input)
    {
        ProvinceKey.Normalize(input).Should().BeEmpty();
    }

    [Fact]
    public void Normalize_PrefixOnly_IsNotStrippedToNothing()
    {
        ProvinceKey.Normalize("Tỉnh").Should().Be("tinh");
    }
}

public sealed class ShippingZoneTests
{
    private static ShippingZone Zone(bool active = true) => new(new ZoneDetails(
        "Nội thành", active, 1, BaseWeightGram: 1000, BaseFee: 20_000m, StepWeightGram: 500, StepFee: 5_000m,
        EtaMinDays: 1, EtaMaxDays: 2));

    [Theory]
    [InlineData(1, 20_000)]
    [InlineData(1000, 20_000)]
    [InlineData(1001, 25_000)] // vượt 1 gram vẫn tính trọn một bước
    [InlineData(1500, 25_000)]
    [InlineData(1501, 30_000)]
    [InlineData(3000, 40_000)]
    public void CalculateFee_AddsOneStepFeePerStartedStep(int weightGram, int expectedFee)
    {
        Zone().CalculateFee(weightGram).Should().Be(expectedFee);
    }

    [Fact]
    public void SetProvinces_NormalizesDeduplicatesAndOnlyAppliesTheDifference()
    {
        var zone = Zone();
        zone.SetProvinces(["Hà Nội", "TP. Hồ Chí Minh", "Thành phố Hà Nội"]);
        zone.Provinces.Select(province => province.ProvinceKey).Should().BeEquivalentTo(["ha noi", "ho chi minh"]);
        var hanoi = zone.Provinces.Single(province => province.ProvinceKey == "ha noi");

        zone.SetProvinces(["Hà Nội", "Đà Nẵng"]);

        zone.Provinces.Select(province => province.ProvinceKey).Should().BeEquivalentTo(["ha noi", "da nang"]);
        zone.Provinces.Should().Contain(hanoi); // giữ nguyên dòng cũ, không xóa rồi tạo lại
    }

    [Fact]
    public void SetProvinces_BlankName_Throws()
    {
        var act = () => Zone().SetProvinces(["  "]);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be("VALIDATION_ERROR");
    }

    [Theory]
    [InlineData(0, 20_000, 500, 5_000, 1, 2)]
    [InlineData(1000, -1, 500, 5_000, 1, 2)]
    [InlineData(1000, 20_000, 0, 5_000, 1, 2)]
    [InlineData(1000, 20_000, 500, 5_000, 3, 2)]
    [InlineData(1000, 20_000, 500, 5_000, 1, 99)]
    public void Update_InvalidTariff_Throws(
        int baseWeight,
        int baseFee,
        int stepWeight,
        int stepFee,
        int etaMin,
        int etaMax)
    {
        var act = () => new ShippingZone(new ZoneDetails(
            "Vùng", true, 0, baseWeight, baseFee, stepWeight, stepFee, etaMin, etaMax));

        act.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Update_BlankName_Throws()
    {
        var act = () => new ShippingZone(new ZoneDetails("  ", true, 0, 1000, 0, 500, 0, 0, 0));

        act.Should().Throw<BusinessException>();
    }
}

public sealed class ShippingQuoteCalculatorTests
{
    private static readonly ShippingSettingsValues Settings = new(
        FreeShippingThreshold: 300_000m, PackagingWeightGram: 100, DefaultBookWeightGram: 400, FlatFee: 30_000m);

    private static readonly ShippingZone Zone = new(new ZoneDetails(
        "Toàn quốc", true, 1, 1000, 25_000m, 500, 5_000m, EtaMinDays: 2, EtaMaxDays: 5));

    [Fact]
    public void TotalWeight_UsesDefaultWeightWhenMissingAndAddsPackaging()
    {
        var items = new[] { new ShippingItem(600, 2), new ShippingItem(null, 1) };

        ShippingQuoteCalculator.TotalWeightGram(items, Settings).Should().Be((600 * 2) + 400 + 100);
    }

    [Fact]
    public void BelowThreshold_ChargesTariffAndReportsMissingAmount()
    {
        var quote = ShippingQuoteCalculator.Calculate(Settings, Zone, [new ShippingItem(500, 2)], 250_000m);

        quote.Fee.Should().Be(30_000m); // 1.100 g → 25.000 + 1 bước 5.000
        quote.BaseFee.Should().Be(30_000m);
        quote.IsFreeByThreshold.Should().BeFalse();
        quote.AmountToFreeShipping.Should().Be(50_000m);
        quote.TotalWeightGram.Should().Be(1100);
        quote.ZoneName.Should().Be("Toàn quốc");
        (quote.EtaMinDays, quote.EtaMaxDays).Should().Be((2, 5));
    }

    [Fact]
    public void AtThreshold_IsFreeButKeepsTariffForReporting()
    {
        var quote = ShippingQuoteCalculator.Calculate(Settings, Zone, [new ShippingItem(500, 2)], 300_000m);

        quote.Fee.Should().Be(0m);
        quote.BaseFee.Should().Be(30_000m);
        quote.IsFreeByThreshold.Should().BeTrue();
        quote.AmountToFreeShipping.Should().Be(0m);
    }

    [Fact]
    public void NoThreshold_NeverFree()
    {
        var quote = ShippingQuoteCalculator.Calculate(
            Settings with { FreeShippingThreshold = null }, Zone, [new ShippingItem(500, 1)], 10_000_000m);

        quote.Fee.Should().Be(25_000m);
        quote.IsFreeByThreshold.Should().BeFalse();
        quote.FreeShippingThreshold.Should().BeNull();
        quote.AmountToFreeShipping.Should().Be(0m);
    }

    [Fact]
    public void NoZone_UsesFlatFeeWithoutEta()
    {
        var quote = ShippingQuoteCalculator.Calculate(Settings, zone: null, [new ShippingItem(500, 1)], 100_000m);

        quote.Fee.Should().Be(30_000m);
        quote.ZoneName.Should().BeNull();
        quote.EtaMinDays.Should().BeNull();
    }
}

public sealed class ShippingValidatorTests
{
    private static UpsertZoneRequest Zone() => new(
        "Nội thành", true, 1, ["Hà Nội", "Hồ Chí Minh"], 1000, 20_000m, 500, 5_000m, 1, 2);

    [Fact]
    public void ValidZone_IsValid()
    {
        var result = new UpsertZoneRequestValidator().Validate(Zone());

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void Zone_DuplicateProvincesAfterNormalization_IsInvalid()
    {
        var result = new UpsertZoneRequestValidator().Validate(Zone() with { Provinces = ["Hà Nội", "TP. Hà Nội"] });

        result.Errors.Should().Contain(error => error.PropertyName == "Provinces");
    }

    [Fact]
    public void Zone_NoProvinces_OrBadTariff_IsInvalid()
    {
        var validator = new UpsertZoneRequestValidator();

        validator.Validate(Zone() with { Provinces = [] }).IsValid.Should().BeFalse();
        validator.Validate(Zone() with { BaseFee = 20_000.5m }).IsValid.Should().BeFalse();
        validator.Validate(Zone() with { EtaMinDays = 5, EtaMaxDays = 2 }).IsValid.Should().BeFalse();
        validator.Validate(Zone() with { StepWeightGram = 0 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Settings_AreValidated()
    {
        var validator = new ShippingSettingsRequestValidator();

        validator.Validate(new ShippingSettingsRequest(300_000m, 100, 400, 30_000m)).IsValid.Should().BeTrue();
        validator.Validate(new ShippingSettingsRequest(null, 100, 400, 30_000m)).IsValid.Should().BeTrue();
        validator.Validate(new ShippingSettingsRequest(-1m, 100, 400, 30_000m)).IsValid.Should().BeFalse();
        validator.Validate(new ShippingSettingsRequest(300_000m, -1, 400, 30_000m)).IsValid.Should().BeFalse();
        validator.Validate(new ShippingSettingsRequest(300_000m, 100, 0, 30_000m)).IsValid.Should().BeFalse();
    }
}
