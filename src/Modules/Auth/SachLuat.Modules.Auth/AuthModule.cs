using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PusherServer;
using SachLuat.Modules.Auth.Application;
using SachLuat.Modules.Auth.Domain;
using SachLuat.Modules.Auth.Infrastructure;
using SachLuat.Modules.Auth.Infrastructure.Persistence;
using SachLuat.Modules.Auth.Infrastructure.Realtime;
using SachLuat.SharedKernel.Configuration;
using SachLuat.SharedKernel.HealthChecks;
using SachLuat.SharedKernel.Modules;
using SachLuat.SharedKernel.Persistence;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth;

public sealed class AuthModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var realtime = configuration.GetSection(RealtimeOptions.SectionName).Get<RealtimeOptions>() ?? new();
        services.Configure<RealtimeOptions>(configuration.GetSection(RealtimeOptions.SectionName));
        AddRealtime(services, realtime);

        var connectionString = configuration.GetRequiredConnectionString();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AuditableEntityInterceptor>();
        services.AddDbContext<AuthDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, AuthDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        // Đăng ký trước AddIdentityCore để thay bộ băm PBKDF2 mặc định.
        services.AddScoped<IPasswordHasher<AppUser>, BCryptPasswordHasher>();
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = AuthRules.PasswordMinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddTokenProvider<AuthenticatorTokenProvider<AppUser>>(TokenOptions.DefaultAuthenticatorProvider);

        AddJwtAuthentication(services);
        AddAuthorizationPolicies(services);

        services.AddScoped<TokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<UserAdminService>();
        services.AddValidatorsFromAssembly(typeof(AuthModule).Assembly, includeInternalTypes: true);
        services.AddHostedService<BootstrapAdminService>();

        services.AddHealthChecks()
            .AddDbContextCheck<AuthDbContext>("auth-db", tags: [HealthCheckTags.Ready]);
    }

    public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync(cancellationToken);
    }

    private static void AddRealtime(IServiceCollection services, RealtimeOptions options)
    {
        if (!options.IsConfigured)
        {
            services.AddSingleton<ISessionNotifier, NullSessionNotifier>();
            return;
        }

        services.AddSingleton<IPusher>(_ => new Pusher(
            options.AppId,
            options.Key,
            options.Secret,
            new PusherOptions { Cluster = options.Cluster, Encrypted = true }));
        services.AddSingleton<ISessionNotifier, PusherSessionNotifier>();
    }

    private static void AddJwtAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                // Giữ nguyên tên claim trong token ("sub", "role") thay vì map sang tên dài kiểu WS-Federation.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = TokenService.CreateSigningKey(jwt),
                    NameClaimType = AuthClaimTypes.Subject,
                    RoleClaimType = AuthClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = ValidateSessionVersionAsync };
            });
    }

    /// <summary>
    /// Token chỉ hợp lệ khi phiên bản phiên trong token (<c>sv</c>) còn bằng phiên bản của tài khoản; đăng nhập nơi khác,
    /// buộc đăng xuất, đặt lại mật khẩu hay khóa tài khoản đều tăng phiên bản nên token cũ bị từ chối ngay.
    /// </summary>
    private static async Task ValidateSessionVersionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!long.TryParse(principal?.FindFirst(AuthClaimTypes.Subject)?.Value, out var userId)
            || !int.TryParse(principal.FindFirst(AuthClaimTypes.SessionVersion)?.Value, out var tokenVersion))
        {
            context.Fail("Token missing session version");
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AuthDbContext>();
        var currentVersion = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (int?)user.SessionVersion)
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (currentVersion != tokenVersion)
        {
            context.Fail("Session ended");
        }
    }

    private static void AddAuthorizationPolicies(IServiceCollection services)
    {
        var authorization = services.AddAuthorizationBuilder();
        foreach (var (policy, roles) in Policies.StaffRoles)
        {
            authorization.AddPolicy(policy, builder => builder
                .RequireAuthenticatedUser()
                .RequireRole(roles)
                .RequireClaim(AuthClaimTypes.AuthenticationMethod, AuthClaimTypes.MultiFactor));
        }
    }
}
