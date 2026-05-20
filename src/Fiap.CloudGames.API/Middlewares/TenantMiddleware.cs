using Fiap.CloudGames.Domain.Tenants;
using Serilog.Context;

namespace Fiap.CloudGames.Api.Middlewares;

public sealed class TenantMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Tenant-Id";
    public const string ContextKey = "TenantId";
    private const string LogPropertyName = "TenantId";
    private const string ClaimType = "tenant_id";

    public async Task InvokeAsync(HttpContext context)
    {
        string? tenantId = null;

        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValue) && !string.IsNullOrWhiteSpace(headerValue))
        {
            tenantId = headerValue.ToString();
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            tenantId = context.User.FindFirst(ClaimType)?.Value;
        }

        var normalized = Tenants.Normalize(tenantId);
        context.Items[ContextKey] = normalized;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
            {
                context.Response.Headers[HeaderName] = normalized;
            }
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(LogPropertyName, normalized))
        {
            await next(context);
        }
    }
}
