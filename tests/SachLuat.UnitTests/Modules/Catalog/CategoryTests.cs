using AwesomeAssertions;
using SachLuat.Modules.Catalog.Domain;
using SachLuat.Modules.Catalog.Domain.Categories;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Catalog;

public sealed class CategoryTests
{
    [Fact]
    public void MoveTo_Self_ThrowsParentInvalid()
    {
        var category = new Category("Dân sự", "dan-su");

        var act = () => category.MoveTo(category);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.CategoryParentInvalidCode);
    }

    [Fact]
    public void MoveTo_Descendant_ThrowsParentInvalid()
    {
        var root = new Category("Dân sự", "dan-su");
        var child = new Category("Hợp đồng", "hop-dong", root);
        var grandChild = new Category("Hợp đồng mua bán", "hop-dong-mua-ban", child);

        var act = () => root.MoveTo(grandChild);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(CatalogErrors.CategoryParentInvalidCode);
    }

    [Fact]
    public void MoveTo_Null_ClearsParent()
    {
        var root = new Category("Dân sự", "dan-su");
        var child = new Category("Hợp đồng", "hop-dong", root);

        child.MoveTo(null);

        child.Parent.Should().BeNull();
        child.ParentId.Should().BeNull();
    }
}
