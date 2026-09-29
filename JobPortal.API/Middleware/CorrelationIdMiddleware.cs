using Serilog.Context;

namespace JobPortal.API.Middleware;

public class CorrelationIdMiddleware
{
    public const string CorrelationIdHeaderName = "X-Request-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrGenerateCorrelationId(context);

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            }
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("TraceId", correlationId))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var values) &&
            !string.IsNullOrWhiteSpace(values.FirstOrDefault()))
        {
            return values.First()!;
        }

        return Guid.NewGuid().ToString("N");
    }
}
