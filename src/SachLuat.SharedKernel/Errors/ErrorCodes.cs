namespace SachLuat.SharedKernel.Errors;

/// <summary>
/// Mã lỗi chung, không thuộc module nào. Mã lỗi nghiệp vụ riêng (OUT_OF_STOCK, COUPON_INVALID, ...)
/// khai báo trong module sở hữu nó.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string Conflict = "CONFLICT";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string InternalError = "INTERNAL_ERROR";
    public const string Unknown = "ERROR";

    public static string FromStatusCode(int statusCode) => statusCode switch
    {
        400 => ValidationError,
        401 => Unauthorized,
        403 => Forbidden,
        404 => NotFound,
        405 => MethodNotAllowed,
        409 => Conflict,
        429 => TooManyRequests,
        >= 500 => InternalError,
        _ => Unknown,
    };
}
