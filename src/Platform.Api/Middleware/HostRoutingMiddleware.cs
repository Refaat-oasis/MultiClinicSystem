using Microsoft.Extensions.Configuration;

namespace Platform.Api.Middleware;

/// <summary>
/// Enforces strict host-based isolation between the Admin host and Clinic host.
/// Routes under /platform/** exist only on the Admin host and return 404 elsewhere.
/// Clinic routes return 404 on the Admin host. Supports optional IP allowlist for the Admin host.
/// </summary>
public sealed class HostRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string[] _allowedAdminIps;

    public HostRoutingMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        ArgumentNullException.ThrowIfNull(configuration);

        string? rawAllowlist = configuration["PlatformAdmin:AllowedIps"];
        _allowedAdminIps = string.IsNullOrWhiteSpace(rawAllowlist)
            ? Array.Empty<string>()
            : rawAllowlist.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        bool isAdminHost = IsAdminHost(context.Request);
        string path = context.Request.Path.Value ?? string.Empty;

        bool isPlatformRoute = path.StartsWith("/platform", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/api/v1/platform", StringComparison.OrdinalIgnoreCase);

        bool isClinicOnlyRoute = path.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
                                 path.StartsWith("/api/v1/c/", StringComparison.OrdinalIgnoreCase) ||
                                 path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase) ||
                                 path.StartsWith("/api/v1/clinics", StringComparison.OrdinalIgnoreCase);

        if (isPlatformRoute && !isAdminHost)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://errors.clinic.local/ROUTE_NOT_FOUND",
                title = "Not Found",
                status = 404,
                detail = "The requested resource was not found on this host.",
                code = "ROUTE_NOT_FOUND"
            });
            return;
        }

        if (isClinicOnlyRoute && isAdminHost)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://errors.clinic.local/ROUTE_NOT_FOUND",
                title = "Not Found",
                status = 404,
                detail = "Clinic endpoints cannot be accessed on the platform admin host.",
                code = "ROUTE_NOT_FOUND"
            });
            return;
        }

        if (isAdminHost && _allowedAdminIps.Length > 0)
        {
            string? remoteIp = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(remoteIp) || !_allowedAdminIps.Contains(remoteIp, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://errors.clinic.local/IP_NOT_ALLOWED",
                    title = "Forbidden",
                    status = 403,
                    detail = "Access denied from this IP address.",
                    code = "IP_NOT_ALLOWED"
                });
                return;
            }
        }

        await _next(context);
    }

    public static bool IsAdminHost(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Host-Type", out var hostType))
        {
            return string.Equals(hostType, "admin", StringComparison.OrdinalIgnoreCase);
        }

        return request.Host.Host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);
    }
}
