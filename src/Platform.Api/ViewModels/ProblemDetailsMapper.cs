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
                       c.Contains("INVALID_CURRENT_PASSWORD", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ACCOUNT_LOCKED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ACCOUNT_NOT_ACTIVATED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("PORTAL_SESSION_INVALID", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("INVALID_OR_EXPIRED_DOWNLOAD_TOKEN", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("DEVICE_REVOKED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("REFRESH_TOKEN", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status401Unauthorized,
            var c when c.Contains("FORBIDDEN", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("INSUFFICIENT_PERMISSION", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("STEP_UP_REQUIRED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("INSPECT_MODE_READ_ONLY", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("CLINIC_SUSPENDED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("OWNER_DEACTIVATED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("DEVICE_APPROVAL_REQUIRED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("MODULE_DISABLED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("DELEGATION_CEILING_EXCEEDED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("PATIENT_ONLINE_BOOKING_BLOCKED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("SYSTEM_ROLE_PROTECTED", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            var c when c.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            var c when c.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("DAY_CLOSED_LOCKED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("DAY_ALREADY_CLOSED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("DAY_ALREADY_REOPENED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("RECEIPT_ALREADY_USED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("COOLDOWN", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("CLINIC_QUOTA_REACHED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ONBOARDING_REQUIRED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("SLUG_TAKEN", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("SLOT_TAKEN", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("REQUEST_EXPIRED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("FINANCIAL_SETTINGS_LOCKED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("MODULE_PREREQUISITE_OFF", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("MODULE_HAS_DEPENDENTS", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ROLE_IN_USE", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("LAST_OWNER_PROTECTED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ROOM_CODE_EXISTS", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("NUMBER_BLOCK_CONTENTION", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("NUMBER_BLOCK_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ALLERGY_WARNING_CONFIRMATION_REQUIRED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("PORTAL_EMAIL_IN_USE", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("PORTAL_ACCOUNT_ALREADY_ACTIVE", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("HOURLY_CODE_CAP_REACHED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("INSUFFICIENT_STOCK", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
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

