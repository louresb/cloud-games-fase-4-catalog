using Serilog.Context;

namespace Fiap.CloudGames.Api.Middlewares;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const string LogPropertyName = "CorrelationId"; 
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items[HeaderName] = correlationId.ToString();

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
            {
                context.Response.Headers[HeaderName] = correlationId.ToString();
            }
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(LogPropertyName, correlationId))
        {
            await _next(context);
        }
    }
}