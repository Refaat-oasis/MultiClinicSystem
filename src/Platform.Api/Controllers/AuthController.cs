using System.Security.Claims;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Transport layer handling authentication endpoints, invitation activations, and session management.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAccountInvitations _accountInvitations;
    private readonly IPlatformAdminService _platformService;

    public AuthController(
        IAuthService authService,
        IAccountInvitations accountInvitations,
        IPlatformAdminService platformService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _accountInvitations = accountInvitations ?? throw new ArgumentNullException(nameof(accountInvitations));
        _platformService = platformService ?? throw new ArgumentNullException(nameof(platformService));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.LoginAsync(
            dto.Email,
            dto.Password,
            dto.DeviceId ?? Guid.NewGuid().ToString("N"),
            dto.DeviceName ?? "Web Browser",
            ipAddress,
            requestId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToProblemDetails(result.Error);
        }

        SetAuthCookies(result.Value.Tokens.AccessToken, result.Value.Tokens.RefreshToken);

        var viewModel = new AuthResponseViewModel(
            result.Value.Tokens.AccessToken,
            result.Value.Tokens.RefreshToken,
            result.Value.Tokens.ExpiresAtUtc,
            new UserViewModel(
                result.Value.User.Id,
                result.Value.User.Email,
                result.Value.User.FullName,
                result.Value.User.Locale,
                result.Value.User.Theme,
                result.Value.User.Roles,
                result.Value.User.ClinicId));

        return Ok(viewModel);
    }

    [HttpPost("activate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Activate([FromBody] ActivateAccountDto dto, CancellationToken cancellationToken)
    {
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.ActivateAccountAsync(
            dto.Email,
            dto.Code,
            dto.Password,
            dto.DeviceId ?? Guid.NewGuid().ToString("N"),
            dto.DeviceName ?? "Web Browser",
            ipAddress,
            requestId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToProblemDetails(result.Error);
        }

        SetAuthCookies(result.Value.Tokens.AccessToken, result.Value.Tokens.RefreshToken);

        var viewModel = new AuthResponseViewModel(
            result.Value.Tokens.AccessToken,
            result.Value.Tokens.RefreshToken,
            result.Value.Tokens.ExpiresAtUtc,
            new UserViewModel(
                result.Value.User.Id,
                result.Value.User.Email,
                result.Value.User.FullName,
                result.Value.User.Locale,
                result.Value.User.Theme,
                result.Value.User.Roles,
                result.Value.User.ClinicId));

        return Ok(viewModel);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ForgotPasswordViewModel), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto, CancellationToken cancellationToken)
    {
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        await _authService.ForgotPasswordAsync(dto.Email, ipAddress, requestId, cancellationToken);

        // Always return 202 Accepted with identical generic confirmation message
        return Accepted(new ForgotPasswordViewModel(
            "إذا كان هذا البريد مسجلاً في النظام، سيصلك رمز إعادة تعيين كلمة المرور خلال لحظات. / If this email is registered, a password reset code has been dispatched."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.ResetPasswordAsync(
            dto.Email,
            dto.Code,
            dto.NewPassword,
            ipAddress,
            requestId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم تغيير كلمة المرور بنجاح. / Password has been reset successfully."));
    }

    [HttpPost("resend-code")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendCode([FromBody] ResendCodeDto dto, CancellationToken cancellationToken)
    {
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.ResendCodeAsync(dto.Email, dto.Purpose, ipAddress, requestId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم إرسال رمز التحقق بنجاح. / Verification code dispatched successfully."));
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto, CancellationToken cancellationToken)
    {
        string rawToken = dto.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken) && Request.Cookies.TryGetValue("__Host-Staff-Refresh", out string? cookieToken))
        {
            rawToken = cookieToken;
        }

        var result = await _authService.RefreshTokenAsync(rawToken, dto.DeviceId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblemDetails(result.Error);
        }

        SetAuthCookies(result.Value.AccessToken, result.Value.RefreshToken);
        return Ok(result.Value);
    }

    [HttpPost("invite")]
    [Authorize]
    [ProducesResponseType(typeof(InviteUserViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Invite([FromBody] InviteUserDto dto, CancellationToken cancellationToken)
    {
        var result = await _accountInvitations.InviteAsync(
            "Staff",
            dto.Email,
            dto.FullName,
            dto.Role,
            dto.ClinicId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToProblemDetails(result.Error);
        }

        return Ok(new InviteUserViewModel(result.Value, "تم إرسال دعوة الانضمام ورمز التفعيل بنجاح. / Invitation sent successfully."));
    }

    [HttpPost("request-email-change")]
    [Authorize]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestEmailChange([FromBody] RequestEmailChangeDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.RequestEmailChangeAsync(userId, dto.NewEmail, ipAddress, requestId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم إرسال رمز التحقق إلى البريد الإلكتروني الجديد. / Verification code sent to the new email."));
    }

    [HttpPost("confirm-email-change")]
    [Authorize]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.ConfirmEmailChangeAsync(userId, dto.Code, ipAddress, requestId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم تحديث البريد الإلكتروني بنجاح. / Email address successfully updated."));
    }

    [HttpPost("revoke-device")]
    [Authorize]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeDevice([FromBody] RevokeDeviceDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.RevokeDeviceAsync(userId, dto.DeviceId, ipAddress, requestId, cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم إبطال الجهاز بنجاح. / Device successfully revoked."));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("__Host-Staff-Token");
        Response.Cookies.Delete("__Host-Staff-Refresh");
        return Ok(new GenericMessageViewModel("تم تسجيل الخروج بنجاح. / Successfully logged out."));
    }

    [HttpGet("access-logs")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<PatientAccessLogItemViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetClinicAccessLogs([FromQuery] Guid? clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _platformService.GetClinicAccessLogsForOwnerAsync(userId, clinicId, cancellationToken);
        if (result.IsFailure)
        {
            return ToProblemDetails(result.Error);
        }

        return Ok(result.Value.Select(l => new PatientAccessLogItemViewModel(
            l.Id,
            l.ActorId,
            l.Reason,
            l.TimestampUtc,
            l.ClinicId,
            l.Details)).ToList());
    }

    private void SetAuthCookies(string accessToken, string refreshToken)
    {
        bool isSecure = Request.IsHttps;

        Response.Cookies.Append("__Host-Staff-Token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddMinutes(15)
        });

        Response.Cookies.Append("__Host-Staff-Refresh", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private ObjectResult ToProblemDetails(Error error)
    {
        int statusCode = error.Code switch
        {
            var c when c.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("CREDENTIALS", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("INVALID_CODE", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("CODE_EXPIRED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ACCOUNT_LOCKED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ACCOUNT_NOT_ACTIVATED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("DEVICE_REVOKED", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("REFRESH_TOKEN", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status401Unauthorized,
            var c when c.Contains("FORBIDDEN", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
            var c when c.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status404NotFound,
            var c when c.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase) ||
                       c.Contains("COOLDOWN", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Message,
            Instance = HttpContext.Request.Path
        };

        problem.Extensions.Add("code", error.Code);
        return StatusCode(statusCode, problem);
    }
}
