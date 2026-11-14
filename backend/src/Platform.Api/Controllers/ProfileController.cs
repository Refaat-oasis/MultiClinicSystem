using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Self-service profile controller authorized exclusively by profile.self policy.
/// Never touches roles, claims, or other users.
/// </summary>
[ApiController]
[Route("api/v1/profile")]
[Authorize(Policy = "profile.self")]
public sealed class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
    }

    [HttpGet]
    [ProducesResponseType(typeof(UserProfileViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _profileService.GetProfileAsync(userId, cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapProfileViewModel(result.Value));
    }

    [HttpPut]
    [ProducesResponseType(typeof(UserProfileViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateProfileAsync(
            userId,
            dto.FullName,
            dto.PhoneNumber,
            dto.Locale,
            dto.Theme,
            dto.AvatarAssetId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapProfileViewModel(result.Value));
    }

    [HttpPost("request-email-change")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestEmailChange([FromBody] RequestEmailChangeDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _profileService.RequestEmailChangeAsync(
            userId,
            dto.NewEmail,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("تم إرسال رمز التحقق إلى البريد الإلكتروني الجديد. / Verification code sent."));
    }

    [HttpPost("confirm-email-change")]
    [ProducesResponseType(typeof(TokenResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string deviceId = User.FindFirst("deviceId")?.Value ?? "web-browser";
        var result = await _profileService.ConfirmEmailChangeAsync(
            userId,
            dto.Code,
            deviceId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        SetStaffCookies(result.Value.AccessToken, result.Value.RefreshToken);
        return Ok(result.Value);
    }

    [HttpPost("change-password")]
    [ProducesResponseType(typeof(TokenResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string deviceId = dto.DeviceId ?? User.FindFirst("deviceId")?.Value ?? "web-browser";
        var result = await _profileService.ChangePasswordAsync(
            userId,
            dto.CurrentPassword,
            dto.NewPassword,
            dto.SignOutOtherDevices,
            deviceId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        SetStaffCookies(result.Value.AccessToken, result.Value.RefreshToken);
        return Ok(result.Value);
    }

    [HttpPost("revoke-device")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeDevice([FromBody] RevokeDeviceDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _profileService.RevokeDeviceAsync(
            userId,
            dto.DeviceId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Items["RequestId"]?.ToString(),
            cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("تم إبطال الجهاز بنجاح. / Device revoked."));
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private void SetStaffCookies(string accessToken, string refreshToken)
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

    private static UserProfileViewModel MapProfileViewModel(UserProfileResult p) => new(
        p.UserId,
        p.Email,
        p.FullName,
        p.PhoneNumber,
        p.AvatarAssetId,
        p.Locale,
        p.Theme,
        p.Devices.Select(d => new UserDeviceItemViewModel(
            d.Id,
            d.DeviceId,
            d.DeviceName,
            d.IsApproved,
            d.IsRevoked,
            d.CreatedAtUtc,
            d.LastSeenAtUtc)).ToList(),
        p.Clinics.Select(c => new UserClinicMembershipViewModel(
            c.ClinicId,
            c.ClinicName,
            c.Slug,
            c.Role,
            c.Status,
            c.OnboardingStatus)).ToList());
}

