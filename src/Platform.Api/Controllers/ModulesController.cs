using System.Security.Claims;
using BuildingBlocks.Domain;
using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.DTOs;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Thin transport controller exposing the read-only clinic module manifest ("My features")
/// while strictly rejecting any non-SuperAdmin attempts to toggle modules.
/// </summary>
[ApiController]
[Route("api/v1/clinics/{clinicId:guid}/modules")]
[Authorize]
public sealed class ModulesController : ControllerBase
{
    private readonly IClinicAuthorizationService _authService;

    public ModulesController(IClinicAuthorizationService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    [HttpGet]
    [ProducesResponseType(typeof(MyModulesManifestViewModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyModules(Guid clinicId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out Guid userId))
        {
            return Unauthorized();
        }

        string? deviceId = User.FindFirst("deviceId")?.Value;
        var result = await _authService.GetMyModulesManifestAsync(userId, clinicId, deviceId, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        var v = result.Value;
        return Ok(new MyModulesManifestViewModel(
            v.ClinicId,
            v.Modules.Select(m => new ClinicModuleStateViewModel(
                m.ModuleKey,
                m.TitleAr,
                m.TitleEn,
                m.DescriptionAr,
                m.DescriptionEn,
                m.Enabled,
                m.Prerequisites,
                m.MissingPrerequisites,
                m.EnabledDependents,
                m.ChangedAtUtc,
                m.Note)).ToList(),
            v.MenuItems.Select(mi => new ModuleMenuItemViewModel(
                mi.ItemKey,
                mi.ModuleKey,
                mi.TitleAr,
                mi.TitleEn,
                mi.Route,
                mi.RequiredPermission,
                mi.Order)).ToList(),
            v.SettingsSections.Select(s => new SettingsSectionViewModel(
                s.SectionKey,
                s.TitleAr,
                s.TitleEn,
                s.RequiredPermission,
                s.ModuleKey)).ToList(),
            v.EffectivePermissions));
    }

    [HttpPut]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult RejectClinicModuleMutation(Guid clinicId, [FromBody] UpdateClinicModulesDto? dto)
    {
        return ProblemDetailsMapper.ToProblemDetails(
            Error.Forbidden("FORBIDDEN_SUPER_ADMIN_ONLY", "Only the platform Super Admin can enable or disable clinic modules."));
    }

    private bool TryGetUserId(out Guid userId)
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }
}
