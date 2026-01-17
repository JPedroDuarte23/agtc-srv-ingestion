using System.Security.Claims;
using AgtcSrvIngestion.Application.Dtos;
using AgtcSrvIngestion.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgtcSrvIngestion.API.Controllers;

[ApiController]
[Route("v1/api/telemetry")]
[Authorize(Roles = "Sensor")]
public class TelemetryController : ControllerBase
{
    private readonly ILogger<TelemetryController> _logger;
    private readonly ITelemetryService _service;

    public TelemetryController(ILogger<TelemetryController> logger, ITelemetryService service)
    {
        _logger = logger;
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> PostTelemetry([FromBody] TelemetryRequest request)
    {
        var deviceId = Guid.Parse(User.FindFirstValue(ClaimTypes.Name)!); 
        await _service.ProcessTelemetryAsync(deviceId, request);

        return Accepted();

    }
}