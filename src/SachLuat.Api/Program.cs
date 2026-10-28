using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using SachLuat.Api.ErrorHandling;
using SachLuat.Api.Modules;
using SachLuat.Api.Security;
using SachLuat.Modules.Auth;
using SachLuat.Modules.Cart;
using SachLuat.Modules.Catalog;
using SachLuat.Modules.Content;
using SachLuat.Modules.Inventory;
using SachLuat.Modules.Notification;
using SachLuat.Modules.Ordering;
using SachLuat.Modules.Promotion;
using SachLuat.Modules.Shipping;
using SachLuat.SharedKernel.Api;
using SachLuat.SharedKernel.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.AddScoped<IValidator<PagedQuery>, PagedQueryValidator<PagedQuery>>();

builder.AddModules(
    new AuthModule(),
    new CatalogModule(),
    new InventoryModule(),
    new CartModule(),
    new OrderingModule(),
    new ContentModule(),
    new ShippingModule(),
    new PromotionModule(),
    new NotificationModule());

var app = builder.Build();

await app.ApplyModuleMigrationsAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<TrustedProxyMiddleware>();

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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// live: tiến trình còn chạy (không check phụ thuộc); ready: sẵn sàng nhận request (check CSDL, Redis, ...).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
});

await app.RunAsync();
