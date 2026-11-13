using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Transport layer for Platform Super Admin authentication, hourly TOTP enrollment, step-up checks, and break-glass recovery.
/// </summary>
[ApiController]
[Route("api/v1/platform/auth")]
public sealed class PlatformAuthController : ControllerBase
{
    private readonly IPlatformAdminAuthService _authService;

    public PlatformAuthController(IPlatformAdminAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PlatformAdminLoginViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] PlatformAdminLoginDto dto, CancellationToken cancellationToken)
    {
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.LoginAsync(
            dto.Email,
            dto.Password,
            dto.HourlyCode,
            dto.RecoveryKey,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var value = result.Value;
        SetPlatformCookie(value.AccessToken);

        return Ok(new PlatformAdminLoginViewModel(
            value.AdminId,
            value.Email,
            value.FullName,
            value.AccessToken,
            value.ExpiresAtUtc,
            value.RequiresEnrollment,
            value.MustChangePassword,
            value.ManualSetupKey,
            value.OtpAuthUri));
    }

    [HttpPost("enroll")]
    [Authorize(Policy = "PlatformAdminEnrollment")]
    [ProducesResponseType(typeof(TotpEnrollmentCompletedViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Enroll([FromBody] PlatformAdminEnrollDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.CompleteEnrollmentAsync(
            adminId,
            dto.NewPassword,
            dto.HourlyCode,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var value = result.Value;
        SetPlatformCookie(value.AccessToken);

        return Ok(new TotpEnrollmentCompletedViewModel(
            value.AdminId,
            value.Email,
            value.AccessToken,
            value.ExpiresAtUtc,
            value.RecoveryKeys));
    }

    [HttpPost("step-up")]
    [Authorize(Policy = "PlatformAdmin")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> StepUp([FromBody] PlatformStepUpDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out Guid adminId))
        {
            return Unauthorized();
        }

        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.VerifyStepUpAsync(
            adminId,
            dto.Password,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تم التحقق الإضافي بنجاح. / Step-up verification succeeded."));
    }

    [HttpPost("break-glass-reset")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BreakGlassReset([FromBody] BreakGlassResetDto dto, CancellationToken cancellationToken)
    {
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        string? requestId = HttpContext.Items["RequestId"]?.ToString();

        var result = await _authService.ResetTwoFactorBreakGlassAsync(
            dto.Email,
            dto.BreakGlassSecret,
            ipAddress,
            requestId,
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        return Ok(new GenericMessageViewModel("تمت إعادة ضبط المصادقة الثنائية بنجاح. / Two-factor authentication has been reset."));
    }

    [HttpPost("logout")]
    [Authorize(Policy = "PlatformAdminEnrollment")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("__Host-Platform-Token");
        return Ok(new GenericMessageViewModel("تم تسجيل الخروج بنجاح. / Successfully logged out."));
    }

    private void SetPlatformCookie(string accessToken)
    {
        Response.Cookies.Append("__Host-Platform-Token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddHours(12)
        });
    }

    private bool TryGetAdminId(out Guid adminId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out adminId);
    }
}

