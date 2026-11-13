using BuildingBlocks.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Api.ViewModels;

public static class ProblemDetailsMapper
{
    public static ObjectResult ToProblemDetails(Error error)
    {
        int statusCode = error.Code switch
        {
            var c when c.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("CREDENTIALS", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("INVALID_CODE", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("CODE_EXPIRED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("CODE_REPLAYED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("TOTP_CODE_REQUIRED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("INVALID_TOTP_CODE", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("STEP_UP_INVALID", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("INVALID_BREAK_GLASS_SECRET", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ACCOUNT_LOCKED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ACCOUNT_NOT_ACTIVATED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("DEVICE_REVOKED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("REFRESH_TOKEN", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status401Unauthorized,
            var c when c.Contains("FORBIDDEN", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("STEP_UP_REQUIRED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("INSPECT_MODE_READ_ONLY", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            var c when c.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            var c when c.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("COOLDOWN", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        var details = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Message
        };
        details.Extensions["code"] = error.Code;

        return new ObjectResult(details) { StatusCode = statusCode };
    }
}

