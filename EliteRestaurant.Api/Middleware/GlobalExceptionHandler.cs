using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace EliteRestaurant.Api.Middleware;

public sealed class GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is OperationCanceledException or Microsoft.Extensions.Hosting.HostAbortedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = context.Response.Headers["X-Correlation-ID"].FirstOrDefault()
                                ?? Activity.Current?.Id
                                ?? Guid.NewGuid().ToString("N")[..8];

            logger.LogError(ex, "Unhandled exception for {Method} {Path} (CorrelationId={CorrelationId})",
                context.Request.Method, context.Request.Path, correlationId);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var payload = new Dictionary<string, object?>
            {
                ["error"] = "An unexpected error occurred",
                ["correlationId"] = correlationId
            };
            var hostEnv = context.RequestServices.GetService<IWebHostEnvironment>();
            if (hostEnv?.IsDevelopment() == true)
                payload["detail"] = ex.Message;

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
