namespace Platform.Api.Middleware;

/// <summary>
/// Enforces host-based isolation between the Admin host and Clinic host.
/// Routes under /platform/** exist only on the Admin host and return 404 elsewhere.
/// </summary>
public sealed class HostRoutingMiddleware
{
    private readonly RequestDelegate _next;

    public HostRoutingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var path = context.Request.Path.Value ?? string.Empty;

        // Check if current host is Admin host (via Host header, domain, or X-Host-Type header for test environments)
        bool isAdminHost = context.Request.Headers.TryGetValue("X-Host-Type", out var hostType)
            ? string.Equals(hostType, "admin", StringComparison.OrdinalIgnoreCase)
            : host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);

        bool isPlatformRoute = path.StartsWith("/platform", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/api/v1/platform", StringComparison.OrdinalIgnoreCase);

        bool isClinicTenantRoute = path.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
                                   path.StartsWith("/api/v1/c/", StringComparison.OrdinalIgnoreCase);

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

        if (isClinicTenantRoute && isAdminHost)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://errors.clinic.local/ROUTE_NOT_FOUND",
                title = "Not Found",
                status = 404,
                detail = "Clinic tenant endpoints cannot be accessed on the platform admin host.",
                code = "ROUTE_NOT_FOUND"
            });
            return;
        }

        await _next(context);
    }
}

