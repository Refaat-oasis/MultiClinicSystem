using System.Security.Claims;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin transport controller for clinic roles, permission catalog, per-user overrides, staff invitations, and device approval.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}")]
[Authorize]
public sealed class RolesPermissionsController : ControllerBase
{
    private readonly IClinicAuthorizationService _authService;

    public RolesPermissionsController(IClinicAuthorizationService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [HttpGet("permissions/catalog")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionDefinitionViewModel>), StatusCodes.Status200OK)]
    public IActionResult GetPermissionCatalog(Guid clinicId)
    {
        var items = _authService.GetPermissionCatalog()
            .Select(p => new PermissionDefinitionViewModel(p.Key, p.ModuleKey, p.Group, p.TitleAr, p.TitleEn, p.IsReserved))
            .ToList();
        return Ok(items);
    }

    [HttpGet("permissions/me")]
    [ProducesResponseType(typeof(UserEffectivePermissionsViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPermissions(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.GetUserEffectivePermissionsAsync(userId, clinicId, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var v = result.Value;
        return Ok(new UserEffectivePermissionsViewModel(
            v.ClinicId, v.UserId, v.RoleName, v.IsOwner, v.EffectivePermissions, v.DormantPermissions, v.EnabledModules));
    }

    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicRoleViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRoles(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.ListRolesAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(MapRole).ToList());
    }

    [HttpPost("roles")]
    [ProducesResponseType(typeof(ClinicRoleViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateRole(Guid clinicId, [FromBody] CreateClinicRoleDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.CreateRoleAsync(
            userId, clinicId, dto.Name, dto.NameAr, dto.NameEn, dto.Permissions,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/roles/{result.Value.RoleId}", MapRole(result.Value));
    }

    [HttpPost("roles/{roleId:guid}/clone")]
    [ProducesResponseType(typeof(ClinicRoleViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> CloneRole(Guid clinicId, Guid roleId, [FromBody] CloneClinicRoleDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.CloneRoleAsync(
            userId, clinicId, roleId, dto.NewName, dto.NewNameAr, dto.NewNameEn,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/roles/{result.Value.RoleId}", MapRole(result.Value));
    }

    [HttpPut("roles/{roleId:guid}/permissions")]
    [ProducesResponseType(typeof(ClinicRoleViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRolePermissions(Guid clinicId, Guid roleId, [FromBody] UpdateRolePermissionsDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.UpdateRolePermissionsAsync(
            userId, clinicId, roleId, dto.Permissions,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapRole(result.Value));
    }

    [HttpDelete("roles/{roleId:guid}")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteRole(Guid clinicId, Guid roleId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.DeleteRoleAsync(
            userId, clinicId, roleId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("تم حذف الدور بنجاح. / Role removed."));
    }

    [HttpGet("staff")]
    [ProducesResponseType(typeof(IReadOnlyList<StaffMemberViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListStaff(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.ListStaffAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(MapStaff).ToList());
    }

    [HttpPost("staff/invite")]
    [ProducesResponseType(typeof(StaffMemberViewModel), StatusCodes.Status201Created)]
    public async Task<IActionResult> InviteStaff(Guid clinicId, [FromBody] InviteStaffMemberDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.InviteStaffAsync(
            userId, clinicId, dto.FullName, dto.Email, dto.RoleName,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Created($"/api/v1/clinics/{clinicId}/staff/{result.Value.UserId}", MapStaff(result.Value));
    }

    [HttpPut("staff/{staffUserId:guid}/role")]
    [ProducesResponseType(typeof(StaffMemberViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignStaffRole(Guid clinicId, Guid staffUserId, [FromBody] AssignStaffRoleDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.AssignStaffRoleAsync(
            userId, clinicId, staffUserId, dto.RoleName,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapStaff(result.Value));
    }

    [HttpPut("staff/{staffUserId:guid}/overrides")]
    [ProducesResponseType(typeof(StaffMemberViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetOverrides(Guid clinicId, Guid staffUserId, [FromBody] SetUserPermissionOverridesDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var inputs = (dto.Overrides ?? new List<PermissionOverrideItemDto>())
            .Select(o => new PermissionOverrideInput(o.PermissionKey, o.Effect))
            .ToList();

        var result = await _authService.SetUserPermissionOverridesAsync(
            userId, clinicId, staffUserId, inputs,
            GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(MapStaff(result.Value));
    }

    [HttpDelete("staff/{staffUserId:guid}")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveStaff(Guid clinicId, Guid staffUserId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.RemoveStaffAsync(
            userId, clinicId, staffUserId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("تم إزالة عضو الفريق بنجاح. / Staff member removed."));
    }

    [HttpGet("devices")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicDeviceItemViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDevices(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.ListClinicDevicesAsync(userId, clinicId, GetDeviceId(), cancellationToken);
        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(result.Value.Select(d => new ClinicDeviceItemViewModel(
                d.Id, d.UserId, d.StaffName, d.StaffEmail, d.DeviceId, d.DeviceName,
                d.IsApproved, d.IsRevoked, d.ApprovedAtUtc, d.RevokedAtUtc, d.CreatedAtUtc)).ToList());
    }

    [HttpPost("devices/{targetDeviceId}/approve")]
    [ProducesResponseType(typeof(GenericMessageViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveDevice(Guid clinicId, string targetDeviceId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        var result = await _authService.ApproveClinicDeviceAsync(
            userId, clinicId, targetDeviceId, GetDeviceId(), GetIp(), GetRequestId(), cancellationToken);

        return result.IsFailure
            ? ProblemDetailsMapper.ToProblemDetails(result.Error)
            : Ok(new GenericMessageViewModel("تم اعتماد الجهاز بنجاح. / Device approved."));
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private string? GetDeviceId() => User.FindFirst("deviceId")?.Value;
    private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? GetRequestId() => HttpContext.Items["RequestId"]?.ToString();

    private static ClinicRoleViewModel MapRole(ClinicRoleResult r) => new(
        r.RoleId, r.ClinicId, r.Name, r.NameAr, r.NameEn, r.TemplateKey,
        r.IsSystemOwner, r.IsEditable, r.AssignedUsersCount,
        r.Permissions, r.EffectivePermissions, r.DormantPermissions);

    private static StaffMemberViewModel MapStaff(StaffMemberResult s) => new(
        s.UserId, s.ClinicId, s.FullName, s.Email, s.RoleName, s.ClinicRoleId,
        s.IsOwner, s.IsActive, s.EmailConfirmed, s.EffectivePermissions, s.DormantPermissions,
        s.Overrides.Select(o => new PermissionOverrideItemViewModel(o.PermissionKey, o.Effect)).ToList());
}

