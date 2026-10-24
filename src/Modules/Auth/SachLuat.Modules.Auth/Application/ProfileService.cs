using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure.Persistence;

namespace SachLuat.Modules.Auth.Application;

/// <summary>Hồ sơ của user đang đăng nhập: thông tin cá nhân, sổ địa chỉ (FR-06), thông tin hóa đơn (FR-07).</summary>
internal sealed class ProfileService(
    AuthService authService,
    AuthDbContext dbContext,
    ILogger<ProfileService> logger)
{
    public async Task<MeResponse> GetAsync(long userId, CancellationToken cancellationToken) =>
        await ToMeResponseAsync(await authService.GetUserAsync(userId), cancellationToken);

    public async Task<MeResponse> UpdateAsync(
        long userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken) =>
        await ToMeResponseAsync(await authService.UpdateProfileAsync(userId, request), cancellationToken);

    public async Task<IReadOnlyList<AddressResponse>> ListAddressesAsync(
        long userId,
        CancellationToken cancellationToken) =>
        ToResponses(await dbContext.UserAddresses.AsNoTracking()
            .Where(address => address.UserId == userId)
            .ToListAsync(cancellationToken));

    public async Task<IReadOnlyList<AddressResponse>> AddAddressAsync(
        long userId,
        UpsertAddressRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var book = await LoadAddressBookAsync(userId, cancellationToken);

        var address = book.Add(ToDetails(request), request.IsDefault);
        dbContext.UserAddresses.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("User {UserId} added address {AddressId}", userId, address.Id);
        return ToResponses(book.Addresses);
    }

    public async Task<IReadOnlyList<AddressResponse>> UpdateAddressAsync(
        long userId,
        long addressId,
        UpsertAddressRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var book = await LoadAddressBookAsync(userId, cancellationToken);

        book.Update(book.Find(addressId), ToDetails(request), request.IsDefault);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponses(book.Addresses);
    }

    public async Task<IReadOnlyList<AddressResponse>> SetDefaultAddressAsync(
        long userId,
        long addressId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var book = await LoadAddressBookAsync(userId, cancellationToken);

        book.MakeDefault(book.Find(addressId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponses(book.Addresses);
    }

    public async Task DeleteAddressAsync(long userId, long addressId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var book = await LoadAddressBookAsync(userId, cancellationToken);

        var address = book.Find(addressId);
        book.Remove(address);
        dbContext.UserAddresses.Remove(address);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("User {UserId} deleted address {AddressId}", userId, addressId);
    }

    public async Task<InvoiceInfoResponse> SaveInvoiceInfoAsync(
        long userId,
        UpsertInvoiceInfoRequest request,
        CancellationToken cancellationToken)
    {
        var details = new InvoiceDetails(request.CompanyName, request.TaxCode, request.Address, request.Email);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(userId, cancellationToken);

        var invoiceInfo = await dbContext.UserInvoiceInfos
            .SingleOrDefaultAsync(info => info.UserId == userId, cancellationToken);
        if (invoiceInfo is null)
        {
            invoiceInfo = new UserInvoiceInfo(userId, details);
            dbContext.UserInvoiceInfos.Add(invoiceInfo);
        }
        else
        {
            invoiceInfo.Update(details);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(invoiceInfo);
    }

    public Task DeleteInvoiceInfoAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.UserInvoiceInfos.Where(info => info.UserId == userId).ExecuteDeleteAsync(cancellationToken);

    private async Task<MeResponse> ToMeResponseAsync(UserResponse user, CancellationToken cancellationToken)
    {
        var defaultAddress = await dbContext.UserAddresses.AsNoTracking()
            .SingleOrDefaultAsync(address => address.UserId == user.Id && address.IsDefault, cancellationToken);
        var invoiceInfo = await dbContext.UserInvoiceInfos.AsNoTracking()
            .SingleOrDefaultAsync(info => info.UserId == user.Id, cancellationToken);

        return new MeResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.PhoneNumber,
            user.BirthDate,
            user.Occupation,
            user.Roles,
            user.TwoFactorEnabled,
            user.RequiresTwoFactorSetup,
            user.IsActive,
            defaultAddress is null ? null : ToResponse(defaultAddress),
            invoiceInfo is null ? null : ToResponse(invoiceInfo));
    }

    // Khóa dòng user trong transaction: các thao tác sổ địa chỉ của cùng user chạy lần lượt, nên không vượt giới hạn
    // số địa chỉ và không có hai địa chỉ mặc định khi bấm nhiều lần.
    private async Task LockUserAsync(long userId, CancellationToken cancellationToken)
    {
        var locked = await dbContext.Database.SqlQuery<long>(
                $"SELECT id AS \"Value\" FROM auth.users WHERE id = {userId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (locked.Count == 0)
        {
            throw AuthErrors.UserNotFound();
        }
    }

    private async Task<AddressBook> LoadAddressBookAsync(long userId, CancellationToken cancellationToken)
    {
        await LockUserAsync(userId, cancellationToken);
        var addresses = await dbContext.UserAddresses
            .Where(address => address.UserId == userId)
            .ToListAsync(cancellationToken);
        return new AddressBook(userId, addresses);
    }

    private static AddressDetails ToDetails(UpsertAddressRequest request) => new(
        request.RecipientName, request.Phone, request.Province, request.Ward, request.StreetAddress);

    // Mặc định đứng đầu, rồi đến địa chỉ cập nhật gần nhất.
    private static List<AddressResponse> ToResponses(IEnumerable<UserAddress> addresses) =>
    [
        .. addresses
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.UpdatedAt)
            .ThenByDescending(address => address.Id)
            .Select(ToResponse),
    ];

    private static AddressResponse ToResponse(UserAddress address) => new(
        address.Id,
        address.RecipientName,
        address.Phone,
        address.Province,
        address.Ward,
        address.StreetAddress,
        address.IsDefault,
        address.UpdatedAt);

    private static InvoiceInfoResponse ToResponse(UserInvoiceInfo info) =>
        new(info.CompanyName, info.TaxCode, info.Address, info.Email, info.UpdatedAt);
}
