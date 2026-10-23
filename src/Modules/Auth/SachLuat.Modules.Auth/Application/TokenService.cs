using System.Buffers.Text;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SachLuat.Modules.Auth.Domain;
using SachLuat.SharedKernel.Security;

namespace SachLuat.Modules.Auth.Application;

internal sealed record AccessToken(string Token, int ExpiresInSeconds);

internal sealed record TwoFactorTicket(long UserId, bool RememberMe);

internal sealed class TokenService(IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider)
{
    // Token tạm sau bước mật khẩu, chỉ dùng cho /auth/login/2fa: audience khác nên không dùng được làm access token.
    private const string TwoFactorAudienceSuffix = ":2fa";
    private const string RememberMeClaim = "remember";
    private static readonly TimeSpan TwoFactorTicketLifetime = TimeSpan.FromMinutes(5);

    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public static SymmetricSecurityKey CreateSigningKey(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));

    public static string HashRefreshToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public AccessToken CreateAccessToken(AppUser user, IEnumerable<string> roles, bool isMultiFactor)
    {
        var claims = new List<Claim>
        {
            new(AuthClaimTypes.Subject, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(AuthClaimTypes.Email, user.Email ?? string.Empty),
            new(AuthClaimTypes.AuthenticationMethod, isMultiFactor ? AuthClaimTypes.MultiFactor : AuthClaimTypes.Password),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(roles.Select(role => new Claim(AuthClaimTypes.Role, role)));

        var lifetime = TimeSpan.FromMinutes(_jwt.AccessTokenMinutes);
        var token = CreateToken(claims, _jwt.Audience, lifetime);
        return new AccessToken(token, (int)lifetime.TotalSeconds);
    }

    public string CreateTwoFactorTicket(long userId, bool rememberMe) => CreateToken(
        [
            new Claim(AuthClaimTypes.Subject, userId.ToString(CultureInfo.InvariantCulture)),
            new Claim(RememberMeClaim, rememberMe ? "true" : "false"),
        ],
        _jwt.Audience + TwoFactorAudienceSuffix,
        TwoFactorTicketLifetime);

    public async Task<TwoFactorTicket?> ValidateTwoFactorTicketAsync(string ticket)
    {
        var result = await _handler.ValidateTokenAsync(ticket, new TokenValidationParameters
        {
            ValidIssuer = _jwt.Issuer,
            ValidAudience = _jwt.Audience + TwoFactorAudienceSuffix,
            IssuerSigningKey = CreateSigningKey(_jwt),
            ClockSkew = TimeSpan.Zero,
        });

        if (!result.IsValid
            || result.ClaimsIdentity.FindFirst(AuthClaimTypes.Subject)?.Value is not { } subject
            || !long.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
        {
            return null;
        }

        var rememberMe = result.ClaimsIdentity.FindFirst(RememberMeClaim)?.Value == "true";
        return new TwoFactorTicket(userId, rememberMe);
    }

    /// <summary>Token ngẫu nhiên 256 bit (trả cho client) và bản băm SHA-256 (lưu DB).</summary>
    public static (string Token, string Hash) CreateRefreshToken()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (token, HashRefreshToken(token));
    }

    private string CreateToken(IEnumerable<Claim> claims, string audience, TimeSpan lifetime)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            SigningCredentials = new SigningCredentials(CreateSigningKey(_jwt), SecurityAlgorithms.HmacSha256),
        });
    }
}
