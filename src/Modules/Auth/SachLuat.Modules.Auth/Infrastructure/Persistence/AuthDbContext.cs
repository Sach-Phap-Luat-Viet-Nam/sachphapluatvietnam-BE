using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Persistence;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Infrastructure.Persistence;

internal sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<AppUser, AppRole, long>(options)
{
    public const string Schema = "auth";

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<AppUser>(user =>
        {
            user.ToTable("users");
            user.Property(u => u.FullName).HasMaxLength(255);
        });
        builder.Entity<AppRole>(role =>
        {
            role.ToTable("roles");
            // Vai trò cố định theo bảng 4.2, seed qua migration. ConcurrencyStamp cố định để migration ổn định.
            role.HasData(Roles.All.Select((name, index) => new AppRole(name)
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
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<Occupation>()
            .HaveConversion<SnakeCaseEnumConverter<Occupation>>()
            .HaveMaxLength(SnakeCaseEnumConverter<Occupation>.MaxLength);
}
