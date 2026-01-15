using System.Security.Claims;
using AgtcSrvIngestion.Application.Dtos;
using AgtcSrvIngestion.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgtcSrvIngestion.API.Controllers;

[ApiController]
[Route("api/telemetry")]
[Authorize(Roles = "Device")] // <--- AQUI ESTÁ A SEGURANÇA! Só sensores entram.
public class TelemetryController : ControllerBase
{
    private readonly ILogger<TelemetryController _logger;
    private readonly ITelemetryService _service;

    public TelemetryController()
    {
    }

    [HttpPost]
    public async Task<IActionResult> PostTelemetry([FromBody] TelemetryRequest request)
    {
        var deviceId = Guid.Parse(User.FindFirstValue(ClaimTypes.Name)!); 
        await _service.ProcessTelemetryAsync(deviceId, request);

        return Accepted();

    }
}