using BuildingBlocks.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Api.ViewModels;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly IClock _clock;

    public HealthController(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<HealthCheckViewModel>(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var model = new HealthCheckViewModel(
            Status: "Healthy",
            TimestampUtc: _clock.UtcNow
        );

        return Ok(model);
    }
}

