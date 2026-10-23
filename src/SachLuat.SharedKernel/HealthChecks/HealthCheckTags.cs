namespace SachLuat.SharedKernel.HealthChecks;

public static class HealthCheckTags
{
    /// <summary>Check phụ thuộc bên ngoài (CSDL, Redis, ...), dùng cho <c>/health/ready</c>.</summary>
    public const string Ready = "ready";
}
