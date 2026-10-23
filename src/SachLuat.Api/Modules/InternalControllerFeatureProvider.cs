using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace SachLuat.Api.Modules;

/// <summary>
/// Cho phép controller <c>internal</c> trong module, để DTO và service của module giữ <c>internal</c>
/// (MVC mặc định chỉ nhận controller public).
/// </summary>
internal sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) =>
        typeInfo is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
        && typeof(ControllerBase).IsAssignableFrom(typeInfo)
        && typeInfo.IsDefined(typeof(ApiControllerAttribute), inherit: true);
}
