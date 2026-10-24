using AwesomeAssertions;
using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Errors;
using Xunit;

namespace SachLuat.UnitTests.Modules.Auth;

public sealed class AddressBookTests
{
    private const long UserId = 42;

    [Fact]
    public void Add_FirstAddress_BecomesDefaultAndTrimsFields()
    {
        var book = new AddressBook(UserId, []);

        var address = book.Add(Details(" Nguyễn Văn A "), makeDefault: false);

        address.IsDefault.Should().BeTrue();
        address.UserId.Should().Be(UserId);
        address.RecipientName.Should().Be("Nguyễn Văn A");
    }

    [Fact]
    public void Add_WithMakeDefault_MovesDefaultFlag()
    {
        var book = new AddressBook(UserId, []);
        var home = book.Add(Details("Nhà"), makeDefault: false);

        var office = book.Add(Details("Cơ quan"), makeDefault: true);

        office.IsDefault.Should().BeTrue();
        home.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Add_WithoutMakeDefault_KeepsCurrentDefault()
    {
        var book = new AddressBook(UserId, []);
        var home = book.Add(Details("Nhà"), makeDefault: false);

        var office = book.Add(Details("Cơ quan"), makeDefault: false);

        home.IsDefault.Should().BeTrue();
        office.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Add_BeyondLimit_ThrowsLimitReached()
    {
        var book = new AddressBook(UserId, []);
        for (var i = 0; i < AddressBook.MaxAddresses; i++)
        {
            book.Add(Details($"Địa chỉ {i}"), makeDefault: false);
        }

        var act = () => book.Add(Details("Thừa"), makeDefault: false);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(AuthErrors.AddressLimitReachedCode);
    }

    [Fact]
    public void Update_DefaultWithMakeDefaultFalse_StaysDefault()
    {
        var book = new AddressBook(UserId, []);
        var home = book.Add(Details("Nhà"), makeDefault: false);

        book.Update(home, Details("Nhà mới"), makeDefault: false);

        home.RecipientName.Should().Be("Nhà mới");
        home.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Remove_Default_PromotesRemainingAddress()
    {
        var book = new AddressBook(UserId, []);
        var home = book.Add(Details("Nhà"), makeDefault: false);
        var office = book.Add(Details("Cơ quan"), makeDefault: false);

        book.Remove(home);

        book.Addresses.Should().ContainSingle().Which.Should().BeSameAs(office);
        office.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Remove_LastAddress_LeavesEmptyBook()
    {
        var book = new AddressBook(UserId, []);
        var home = book.Add(Details("Nhà"), makeDefault: false);

        book.Remove(home);

        book.Addresses.Should().BeEmpty();
    }

    [Fact]
    public void Find_UnknownId_ThrowsNotFound()
    {
        var book = new AddressBook(UserId, []);

        var act = () => book.Find(999);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(AuthErrors.AddressNotFoundCode);
    }

    [Fact]
    public void MakeDefault_AddressFromAnotherBook_ThrowsNotFound()
    {
        var book = new AddressBook(UserId, []);
        book.Add(Details("Nhà"), makeDefault: false);
        var stranger = new AddressBook(7, []).Add(Details("Người khác"), makeDefault: false);

        var act = () => book.MakeDefault(stranger);

        act.Should().Throw<BusinessException>().Which.Code.Should().Be(AuthErrors.AddressNotFoundCode);
    }

    private static AddressDetails Details(string recipientName) =>
        new(recipientName, "0901234567", "Hà Nội", "Phường Cửa Nam", "12 Hàng Bài");
}
