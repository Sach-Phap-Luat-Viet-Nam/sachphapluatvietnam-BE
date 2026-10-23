namespace SachLuat.SharedKernel.Api;

public static class ApiRoutes
{
    /// <summary>Tiền tố cho mọi controller: <c>[Route(ApiRoutes.V1 + "/books")]</c>.</summary>
    public const string V1 = "api/v1";

    /// <summary>Tiền tố cho API trang quản trị.</summary>
    public const string AdminV1 = V1 + "/admin";
}
