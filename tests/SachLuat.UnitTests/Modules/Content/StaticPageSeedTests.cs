using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SachLuat.Modules.Content.Application;
using SachLuat.Modules.Content.Infrastructure.Persistence.Migrations;
using Xunit;

namespace SachLuat.UnitTests.Modules.Content;

/// <summary>Hai trang chính sách seed trong migration phải hợp lệ như khi Admin tạo qua API.</summary>
public sealed partial class StaticPageSeedTests
{
    private static readonly IReadOnlyList<UpsertStaticPageRequest> Seeded = ReadSeed();

    [Fact]
    public void Seed_ContainsBothPolicyPages_Published()
    {
        Seeded.Select(page => page.Slug).Should().BeEquivalentTo("dieu-khoan-su-dung", "chinh-sach-bao-mat");
        Seeded.Should().OnlyContain(page => page.IsPublished);
    }

    [Fact]
    public void Seed_PassesRequestValidation()
    {
        var validator = new UpsertStaticPageRequestValidator();

        foreach (var page in Seeded)
        {
            var result = validator.Validate(page);
            result.IsValid.Should().BeTrue($"{page.Slug}: {string.Join("; ", result.Errors)}");
        }
    }

    [Fact]
    public void Seed_EverySectionHeadingHasUniqueAnchor()
    {
        foreach (var page in Seeded)
        {
            var headings = HeadingPattern().Matches(page.ContentHtml);
            var anchors = AnchorPattern().Matches(page.ContentHtml).Select(match => match.Groups[1].Value).ToList();

            headings.Should().NotBeEmpty(page.Slug);
            anchors.Should().HaveCount(headings.Count, $"mọi <h2> của {page.Slug} đều có id cho mục lục");
            anchors.Should().OnlyHaveUniqueItems(page.Slug);
        }
    }

    private static List<UpsertStaticPageRequest> ReadSeed()
    {
        var insert = new AddStaticPages().UpOperations.OfType<InsertDataOperation>().Single();
        var columns = insert.Columns.ToList();
        var values = insert.Values ?? throw new InvalidOperationException("Seed không có dữ liệu");

        object? Cell(int row, string column) => values[row, columns.IndexOf(column)];

        return [.. Enumerable.Range(0, values.GetLength(0)).Select(row => new UpsertStaticPageRequest(
            (string)Cell(row, "slug")!,
            (string)Cell(row, "title")!,
            (string?)Cell(row, "description"),
            (string)Cell(row, "content_html")!,
            (bool)Cell(row, "is_published")!))];
    }

    [GeneratedRegex("<h2[ >]")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex("<h2 id=\"([a-z0-9-]+)\">")]
    private static partial Regex AnchorPattern();
}
