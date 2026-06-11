using System.Net;
using System.Text.Json;
using FluentValidation;

namespace InventoryManager.API.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns a consistent JSON error response.
/// Without this, ASP.NET would return its default HTML error page or expose stack traces.
/// This middleware ensures every error response has the shape:
/// { "status": 500, "message": "...", "traceId": "..." }
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message, errors) = exception switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                (object)ve.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key.Length > 0 ? char.ToLower(g.Key[0]) + g.Key[1..] : g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray())),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "You do not have permission.", (object?)null),
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message, (object?)null),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message, (object?)null),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message, (object?)null),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.", (object?)null)
        };

        context.Response.StatusCode = (int)statusCode;

        var response = errors is not null
            ? new { status = (int)statusCode, message, errors, traceId = context.TraceIdentifier }
            : (object)new { status = (int)statusCode, message, traceId = context.TraceIdentifier };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
