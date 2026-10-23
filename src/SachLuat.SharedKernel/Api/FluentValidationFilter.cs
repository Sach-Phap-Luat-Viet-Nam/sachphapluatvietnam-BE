using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace SachLuat.SharedKernel.Api;

/// <summary>
/// Chạy <see cref="IValidator{T}"/> cho mọi tham số action có validator đăng ký trong DI;
/// sai thì trả <c>ValidationProblemDetails</c> 400 (code <c>VALIDATION_ERROR</c>).
/// </summary>
public sealed class FluentValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            foreach (var error in result.Errors)
            {
                context.ModelState.AddModelError(ToCamelCasePath(error.PropertyName), error.ErrorMessage);
            }
        }

        if (!context.ModelState.IsValid)
        {
            var problemDetails = services.GetRequiredService<ProblemDetailsFactory>()
                .CreateValidationProblemDetails(
                    context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest);
            context.Result = new ObjectResult(problemDetails)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
            return;
        }

        await next();
    }

    // "Lines[0].UnitCost" -> "lines[0].unitCost" để khớp tên field JSON.
    private static string ToCamelCasePath(string propertyName) => string.Join(
        '.',
        propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
