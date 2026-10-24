using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Persistence;
using SecurityRoles = SachLuat.SharedKernel.Security.Roles;

namespace SachLuat.Modules.Auth.Infrastructure.Persistence;

internal sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<AppUser, AppRole, long>(options)
{
    public const string Schema = "auth";

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();

    public DbSet<UserInvoiceInfo> UserInvoiceInfos => Set<UserInvoiceInfo>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<AppUser>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.FullName).HasMaxLength(255);
            user.Property(u => u.AvatarUrl).HasMaxLength(AppUser.AvatarUrlMaxLength);
            user.Property(u => u.SessionVersion).HasDefaultValue(0);
            // Không ghi cột này khi lưu user: một lần UserManager.UpdateAsync với bản cũ trong bộ nhớ sẽ ghi đè phiên
            // bản vừa tăng và làm token đã bị thu hồi sống lại.
            user.Property(u => u.SessionVersion).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        });
        builder.Entity<AppRole>(role =>
        {
            role.ToTable("roles");
            // Vai trò cố định theo bảng 4.2, seed qua migration. ConcurrencyStamp cố định để migration ổn định.
            role.HasData(SecurityRoles.All.Select((name, index) => new AppRole(name)
            {
                Id = index + 1,
                NormalizedName = name.ToUpperInvariant(),
                ConcurrencyStamp = name,
            }));
        });
        builder.Entity<IdentityUserRole<long>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<long>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<long>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<long>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<long>>().ToTable("role_claims");

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("refresh_tokens");
            token.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength();
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => t.UserId);
            token.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserAddress>(address =>
        {
            address.ToTable("user_addresses");
            address.Property(a => a.RecipientName).HasMaxLength(UserAddress.RecipientNameMaxLength);
            address.Property(a => a.Phone).HasMaxLength(UserAddress.PhoneMaxLength);
            address.Property(a => a.Province).HasMaxLength(UserAddress.ProvinceMaxLength);
            address.Property(a => a.Ward).HasMaxLength(UserAddress.WardMaxLength);
            address.Property(a => a.StreetAddress).HasMaxLength(UserAddress.StreetAddressMaxLength);
            address.HasIndex(a => a.UserId);
            address.HasOne<AppUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserInvoiceInfo>(info =>
        {
            info.ToTable("user_invoice_infos");
            info.HasKey(i => i.UserId);
            info.Property(i => i.UserId).ValueGeneratedNever();
            info.Property(i => i.CompanyName).HasMaxLength(UserInvoiceInfo.CompanyNameMaxLength);
            info.Property(i => i.TaxCode).HasMaxLength(UserInvoiceInfo.TaxCodeMaxLength);
            info.Property(i => i.Address).HasMaxLength(UserInvoiceInfo.AddressMaxLength);
            info.Property(i => i.Email).HasMaxLength(UserInvoiceInfo.EmailMaxLength);
            info.HasOne<AppUser>().WithOne().HasForeignKey<UserInvoiceInfo>(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Occupation>()
            .HaveConversion<SnakeCaseEnumConverter<Occupation>>()
            .HaveMaxLength(SnakeCaseEnumConverter<Occupation>.MaxLength);
        configurationBuilder.Properties<ColorTheme>()
            .HaveConversion<SnakeCaseEnumConverter<ColorTheme>>()
            .HaveMaxLength(SnakeCaseEnumConverter<ColorTheme>.MaxLength);
    }
}
