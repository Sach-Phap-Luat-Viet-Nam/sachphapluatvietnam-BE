using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using SachLuat.Modules.Auth.Infrastructure.Realtime;
using Xunit;

namespace SachLuat.IntegrationTests.EndToEnd;

/// <summary>
/// Host thật với PostgreSQL + Redis. Cần biến môi trường <see cref="DatabaseVariable"/> và
/// <see cref="RedisVariable"/>; thiếu thì các test bị skip. Database <c>sachluat_e2e</c> bị xóa và tạo lại mỗi lần chạy.
/// </summary>
public sealed class EndToEndFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DatabaseVariable = "SACHLUAT_TEST_DB";
    public const string RedisVariable = "SACHLUAT_TEST_REDIS";
    public const string AdminEmail = "admin@e2e.test";
    public const string AdminPassword = "e2e-admin-password";

    // Hằng số (không lấy từ input) nên ghép thẳng vào câu DROP/CREATE DATABASE an toàn.
    private const string DatabaseName = "sachluat_e2e";

    private readonly string? _connectionString = BuildConnectionString();
    private readonly string? _redis = Environment.GetEnvironmentVariable(RedisVariable);

    /// <summary>Đồng hồ giả để tua thời gian (đơn quá hạn thanh toán). Bắt đầu ở thời điểm thật.</summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Ghi lại các thông báo kết thúc phiên thay cho Pusher thật.</summary>
    internal RecordingSessionNotifier SessionNotices { get; } = new();

    public bool IsConfigured => _connectionString is not null && !string.IsNullOrWhiteSpace(_redis);

    public async ValueTask InitializeAsync()
    {
        if (!IsConfigured)
        {
            return;
        }

        // Xóa và tạo lại database để mỗi lần chạy bắt đầu sạch (admin chưa bật 2FA, chưa có đơn).
        // Kết nối qua template1 vì không phải cụm PostgreSQL nào cũng còn database "postgres".
        var admin = new NpgsqlConnectionStringBuilder(_connectionString) { Database = "template1", Pooling = false };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        foreach (var sql in new[]
                 {
                     $"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)",
                     $"CREATE DATABASE {DatabaseName}",
                 })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    public void SkipIfNotConfigured() => Assert.SkipUnless(
        IsConfigured, $"Set {DatabaseVariable} and {RedisVariable} to run end-to-end tests (see docs/setup.md).");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _connectionString ?? "Host=unused");
        builder.UseSetting("ConnectionStrings:Redis", _redis ?? "unused");
        builder.UseSetting("Jwt:SigningKey", ApiFactory.TestSigningKey);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", IsConfigured ? "true" : "false");
        builder.UseSetting("Ordering:EnableExpiredPaymentSweep", "false");
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "1000");
        builder.UseSetting("Cart:KeyPrefix", $"e2e:{Guid.NewGuid():N}:cart:");
        builder.UseSetting("Auth:BootstrapAdmin:Email", AdminEmail);
        builder.UseSetting("Auth:BootstrapAdmin:Password", AdminPassword);
        // Luôn coi như chưa cấu hình Cloudinary (kể cả khi user-secrets của máy dev có), để test lưu được link ảnh bất kỳ.
        builder.UseSetting("Cloudinary:ApiSecret", string.Empty);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<ISessionNotifier>(SessionNotices);
        });
    }

    private static string? BuildConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(DatabaseVariable);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : new NpgsqlConnectionStringBuilder(value) { Database = DatabaseName }.ConnectionString;
    }
}

internal sealed class RecordingSessionNotifier : ISessionNotifier
{
    private readonly List<SessionNotice> _notices = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<SessionNotice> For(long userId)
    {
        lock (_gate)
        {
            return _notices.Where(notice => notice.UserId == userId).ToList();
        }
    }

    public Task NotifySessionEndedAsync(
        long userId,
        int sessionVersion,
        SessionEndReason reason,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _notices.Add(new SessionNotice(userId, sessionVersion, reason));
        }

        return Task.CompletedTask;
    }
}

internal sealed record SessionNotice(long UserId, int Version, SessionEndReason Reason);
