using AwesomeAssertions;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Books;
using SachLuat.Modules.Catalog.Domain.Editions;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class BookTests
{
    private static readonly EditionPublishingInfo Edition2025 = new("9780306406157", "NXB Tư pháp", "123/QĐ-NXBTP");
    private static readonly EditionPublishingInfo Edition2026 = new("9786041234567", "NXB Tư pháp", "456/QĐ-NXBTP");

    [Fact]
    public void Constructor_NewBook_IsDraft()
    {
        var book = new Book("Bộ luật Dân sự", "bo-luat-dan-su", BookType.Code);

        book.Status.Should().Be(BookStatus.Draft);
        book.LatestEdition.Should().BeNull();
    }

    [Fact]
    public void Constructor_InvalidSlug_ThrowsSlugInvalid()
    {
        var act = () => new Book("Bộ luật Dân sự", "Bộ Luật", BookType.Code);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.SlugInvalidCode);
    }

    [Fact]
    public void SetLatestEdition_KeepsOnlyOneLatest()
    {
        var book = new Book("Bộ luật Dân sự", "bo-luat-dan-su", BookType.Code);
        var oldEdition = book.AddEdition(Edition2025);
        var newEdition = book.AddEdition(Edition2026);

        book.SetLatestEdition(oldEdition);
        book.SetLatestEdition(newEdition);

        book.LatestEdition.Should().BeSameAs(newEdition);
        oldEdition.IsLatest.Should().BeFalse();
        book.Editions.Count(edition => edition.IsLatest).Should().Be(1);
    }

    [Fact]
    public void SetLatestEdition_EditionOfAnotherBook_ThrowsNotFound()
    {
        var book = new Book("Bộ luật Dân sự", "bo-luat-dan-su", BookType.Code);
        var otherBook = new Book("Luật Đất đai", "luat-dat-dai", BookType.Consolidated);
        var foreignEdition = otherBook.AddEdition(Edition2026);

        var act = () => book.SetLatestEdition(foreignEdition);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.EditionNotFoundCode);
    }

    [Fact]
    public void AddEdition_SameIsbnTwice_ThrowsDuplicate()
    {
        var book = new Book("Bộ luật Dân sự", "bo-luat-dan-su", BookType.Code);
        book.AddEdition(Edition2025);

        var act = () => book.AddEdition(Edition2025 with { Isbn = "978-0-306-40615-7" });

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.IsbnDuplicateCode);
    }

    [Fact]
    public void AddEdition_MissingPublishingDecision_ThrowsValidationError()
    {
        var book = new Book("Bộ luật Dân sự", "bo-luat-dan-su", BookType.Code);

        var act = () => book.AddEdition(Edition2025 with { PublishingDecisionNo = " " });

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact]
    public void AddEdition_InvalidIsbn_ThrowsIsbnInvalid()
    {
        var book = new Book("Bộ luật Dân sự", "bo-luat-dan-su", BookType.Code);

        var act = () => book.AddEdition(Edition2025 with { Isbn = "9780306406158" });

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.IsbnInvalidCode);
    }

    [Fact]
    public void UpdateDetails_DeduplicatesTargetAudiences()
    {
        var book = new Book("Bộ luật Dân sự", "bo-luat-dan-su", BookType.Code);

        book.UpdateDetails(
            BookType.Code,
            description: null,
            [TargetAudience.Lawyer, TargetAudience.Student, TargetAudience.Lawyer],
            metaTitle: null,
            metaDescription: null);

        book.TargetAudiences.Should().Equal(TargetAudience.Student, TargetAudience.Lawyer);
    }
}
