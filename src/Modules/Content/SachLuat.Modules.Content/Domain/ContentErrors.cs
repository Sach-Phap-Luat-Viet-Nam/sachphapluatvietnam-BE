using Microsoft.AspNetCore.Http;
using SachLuat.SharedKernel.Errors;

namespace SachLuat.Modules.Content.Domain;

internal static class ContentErrors
{
    public static BusinessException Invalid(string message) =>
        new(ErrorCodes.ValidationError, message, StatusCodes.Status400BadRequest);

    public static BusinessException PopupNotFound() =>
        new(ErrorCodes.NotFound, "Không tìm thấy popup", StatusCodes.Status404NotFound);
}
