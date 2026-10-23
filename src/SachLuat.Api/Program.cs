using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SachLuat.Api.ErrorHandling;
using SachLuat.Api.Modules;
using SachLuat.Modules.Catalog;
using SachLuat.SharedKernel.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.AddModules(
    new CatalogModule());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// live: tiến trình còn chạy (không check phụ thuộc); ready: sẵn sàng nhận request (check CSDL, ...).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
});

app.Run();
