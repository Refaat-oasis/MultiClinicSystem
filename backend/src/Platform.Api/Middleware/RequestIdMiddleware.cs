using BuildingBlocks.Domain;

namespace Platform.Api.Middleware;

/// <summary>
/// Ensures every incoming request is assigned a stable GuidV7 correlation identifier.
/// </summary>
public sealed class RequestIdMiddleware
{
    public const string HeaderName = "X-Request-Id";
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = GuidV7.NewGuid().ToString();
        }

        context.TraceIdentifier = correlationId.ToString();
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context);
    }
}

