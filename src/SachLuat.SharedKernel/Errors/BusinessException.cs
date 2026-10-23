using Microsoft.AspNetCore.Http;

namespace SachLuat.SharedKernel.Errors;

/// <summary>
/// Lỗi nghiệp vụ có mã ổn định (ví dụ <c>OUT_OF_STOCK</c>) để frontend xử lý.
/// Được global exception handler chuyển thành <c>ProblemDetails</c> với field <c>code</c>.
/// </summary>
/// <param name="code">Mã lỗi viết hoa, phân tách bằng gạch dưới.</param>
/// <param name="message">Thông báo tiếng Việt hiển thị cho khách.</param>
/// <param name="statusCode">Mã HTTP trả về, mặc định 422.</param>
public class BusinessException(
    string code,
    string message,
    int statusCode = StatusCodes.Status422UnprocessableEntity) : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode { get; } = statusCode;
}
