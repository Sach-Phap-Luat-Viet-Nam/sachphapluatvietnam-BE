using AwesomeAssertions;
using SachLuat.Modules.Catalog.Application.Admin;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class UpsertPriceScheduleRequestValidatorTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void ValidRequest_IsValid()
    {
        var result = Validate(new UpsertPriceScheduleRequest(150_000m, StartsAt, StartsAt.AddDays(7), "Sale"));

        result.IsValid.Should().BeTrue(string.Join("; ", result.Errors));
    }

    [Fact]
    public void EndsAtNotAfterStartsAt_IsInvalid()
    {
        var result = Validate(new UpsertPriceScheduleRequest(150_000m, StartsAt, StartsAt, null));

        result.Errors.Should().ContainSingle(error => error.PropertyName == "EndsAt");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(150_000.5)]
    public void SalePriceNotWholePositiveVnd_IsInvalid(double salePrice)
    {
        var result = Validate(new UpsertPriceScheduleRequest((decimal)salePrice, StartsAt, StartsAt.AddDays(1), null));

        result.Errors.Should().Contain(error => error.PropertyName == "SalePrice");
    }

    [Fact]
    public void NoteTooLong_IsInvalid()
    {
        var result = Validate(new UpsertPriceScheduleRequest(
            150_000m, StartsAt, StartsAt.AddDays(1), new string('a', 256)));

        result.Errors.Should().ContainSingle(error => error.PropertyName == "Note");
    }

    private static FluentValidation.Results.ValidationResult Validate(UpsertPriceScheduleRequest request) =>
        new UpsertPriceScheduleRequestValidator().Validate(request);
}
