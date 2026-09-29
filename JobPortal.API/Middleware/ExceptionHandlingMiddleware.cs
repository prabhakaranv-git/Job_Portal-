using System.Net;
using System.Text.Json;
using JobPortal.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace JobPortal.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.Items["CorrelationId"]?.ToString() ?? context.TraceIdentifier;

        ProblemDetails problemDetails;
        int statusCode;

        switch (exception)
        {
            case ValidationException validationEx:
                statusCode = (int)HttpStatusCode.BadRequest;
                var validationProblem = new ValidationProblemDetails(validationEx.Errors)
                {
                    Type = "https://api.jobportal.com/errors/validation-error",
                    Title = "Validation Failed",
                    Status = statusCode,
                    Detail = validationEx.Message,
                    Instance = context.Request.Path
                };
                validationProblem.Extensions["traceId"] = traceId;
                problemDetails = validationProblem;
                _logger.LogWarning(exception, "Validation exception occurred: {Message}. TraceId: {TraceId}", validationEx.Message, traceId);
                break;

            case EntityNotFoundException notFoundEx:
                statusCode = (int)HttpStatusCode.NotFound;
                problemDetails = new ProblemDetails
                {
                    Type = "https://api.jobportal.com/errors/not-found",
                    Title = "Resource Not Found",
                    Status = statusCode,
                    Detail = notFoundEx.Message,
                    Instance = context.Request.Path
                };
                problemDetails.Extensions["traceId"] = traceId;
                _logger.LogInformation("Not found exception: {Message}. TraceId: {TraceId}", notFoundEx.Message, traceId);
                break;

            case ConflictException conflictEx:
                statusCode = (int)HttpStatusCode.Conflict;
                problemDetails = new ProblemDetails
                {
                    Type = "https://api.jobportal.com/errors/conflict",
                    Title = "Conflict",
                    Status = statusCode,
                    Detail = conflictEx.Message,
                    Instance = context.Request.Path
                };
                problemDetails.Extensions["traceId"] = traceId;
                _logger.LogWarning("Conflict exception: {Message}. TraceId: {TraceId}", conflictEx.Message, traceId);
                break;

            case UnauthorizedDomainException authEx:
                statusCode = (int)HttpStatusCode.Unauthorized;
                problemDetails = new ProblemDetails
                {
                    Type = "https://api.jobportal.com/errors/unauthorized",
                    Title = "Unauthorized",
                    Status = statusCode,
                    Detail = authEx.Message,
                    Instance = context.Request.Path
                };
                problemDetails.Extensions["traceId"] = traceId;
                _logger.LogWarning("Unauthorized domain exception: {Message}. TraceId: {TraceId}", authEx.Message, traceId);
                break;

            case ForbiddenException forbiddenEx:
                statusCode = (int)HttpStatusCode.Forbidden;
                problemDetails = new ProblemDetails
                {
                    Type = "https://api.jobportal.com/errors/forbidden",
                    Title = "Forbidden",
                    Status = statusCode,
                    Detail = forbiddenEx.Message,
                    Instance = context.Request.Path
                };
                problemDetails.Extensions["traceId"] = traceId;
                _logger.LogWarning("Forbidden exception: {Message}. TraceId: {TraceId}", forbiddenEx.Message, traceId);
                break;

            case DomainException domainEx:
                statusCode = domainEx.StatusCode;
                problemDetails = new ProblemDetails
                {
                    Type = "https://api.jobportal.com/errors/domain-error",
                    Title = "Domain Error",
                    Status = statusCode,
                    Detail = domainEx.Message,
                    Instance = context.Request.Path
                };
                problemDetails.Extensions["traceId"] = traceId;
                _logger.LogWarning(domainEx, "Domain exception with status {StatusCode}: {Message}. TraceId: {TraceId}", statusCode, domainEx.Message, traceId);
                break;

            default:
                statusCode = (int)HttpStatusCode.InternalServerError;
                var detail = _environment.IsProduction()
                    ? "An unexpected internal server error occurred. Please contact support referencing the trace ID."
                    : $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";

                problemDetails = new ProblemDetails
                {
                    Type = "https://api.jobportal.com/errors/internal-server-error",
                    Title = "An unexpected error occurred.",
                    Status = statusCode,
                    Detail = detail,
                    Instance = context.Request.Path
                };
                problemDetails.Extensions["traceId"] = traceId;
                _logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}", traceId);
                break;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, problemDetails.GetType(), JsonOptions));
    }
}
