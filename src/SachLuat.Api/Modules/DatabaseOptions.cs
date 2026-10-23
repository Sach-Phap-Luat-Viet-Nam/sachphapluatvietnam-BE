namespace SachLuat.Api.Modules;

internal sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Tự áp migration khi khởi động. Chỉ bật ở local/test, không bật ở production.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}
