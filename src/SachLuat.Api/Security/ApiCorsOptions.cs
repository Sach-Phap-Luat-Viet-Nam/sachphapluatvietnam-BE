namespace SachLuat.Api.Security;

internal sealed class ApiCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>Origin của frontend được gọi API kèm cookie, dạng <c>scheme://host[:port]</c>.</summary>
    public string[] AllowedOrigins { get; set; } = [];
}
