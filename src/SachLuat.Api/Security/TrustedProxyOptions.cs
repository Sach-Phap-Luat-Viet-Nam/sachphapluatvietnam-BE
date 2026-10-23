namespace SachLuat.Api.Security;

internal sealed class TrustedProxyOptions
{
    public const string SectionName = "TrustedProxy";

    public const int MinSecretLength = 32;

    /// <summary>
    /// Secret chung với proxy FE (Next.js). Để trống thì không tin header <c>X-Client-Ip</c>.
    /// </summary>
    public string Secret { get; set; } = string.Empty;
}
