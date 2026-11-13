using Core.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

/// <summary>
/// Resolves public clinic slugs and permanent canonical redirects for historical slugs.
/// </summary>
[ApiController]
[Route("api/v1/c")]
public sealed class PublicClinicDirectoryController : ControllerBase
{
    private readonly IClinicManagementService _clinicService;

    public PublicClinicDirectoryController(IClinicManagementService clinicService)
    {
        _clinicService = clinicService ?? throw new ArgumentNullException(nameof(clinicService));
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicClinicResolutionViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveBySlug(string slug, CancellationToken cancellationToken)
    {
        var result = await _clinicService.ResolvePublicSlugAsync(slug, cancellationToken);
        if (result.IsFailure)
        {
            return ProblemDetailsMapper.ToProblemDetails(result.Error);
        }

        if (result.Value.IsRedirect)
        {
            Response.Headers["X-Canonical-Slug"] = result.Value.CurrentSlug;
            Response.Headers["X-Redirect-Target"] = result.Value.RedirectUrl;
        }

        return Ok(new PublicClinicResolutionViewModel(
            result.Value.ClinicId,
            result.Value.Name,
            result.Value.CurrentSlug,
            result.Value.RequestedSlug,
            result.Value.IsRedirect,
            result.Value.RedirectUrl,
            result.Value.LogoAssetId));
    }
}

