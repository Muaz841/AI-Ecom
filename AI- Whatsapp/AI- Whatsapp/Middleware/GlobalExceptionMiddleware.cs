using System.Net;
using System.Text.Json;
using EcomAI.Platform.Business.Interfaces;
using FluentValidation;

namespace EcomAI.Platform.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IApplicationLogger appLogger)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, appLogger);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, IApplicationLogger appLogger)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException => (HttpStatusCode.BadRequest, "Validation failed"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
            ArgumentException => (HttpStatusCode.BadRequest, "Invalid request"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Operation rejected"),
            _ => (HttpStatusCode.InternalServerError, "Internal server error")
        };

        appLogger.Error(
            exception,
            "Unhandled exception for {Method} {Path} TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var payload = new
        {
            title,
            detail = exception.Message,
            status = (int)statusCode,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
