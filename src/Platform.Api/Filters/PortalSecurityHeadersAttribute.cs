using Microsoft.AspNetCore.Mvc.Filters;

namespace Platform.Api.Filters;

/// <summary>
/// Enforces no-store caching and noindex directives on all Patient Portal HTTP responses.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PortalSecurityHeadersAttribute : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers.Pragma = "no-cache";
        context.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        base.OnResultExecuting(context);
    }
}

