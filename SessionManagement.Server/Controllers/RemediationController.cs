using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RemediationController : ControllerBase
    {
        private readonly RemediationService _remediationService;

        public RemediationController(RemediationService remediationService)
        {
            _remediationService = remediationService;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteRemediation([FromBody] RemediationRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ClientId))
            {
                return BadRequest("Invalid remediation request. ClientId is required.");
            }

            var log = await _remediationService.ExecuteRemediationAsync(request);
            return Ok(log);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] string? clientId)
        {
            var logs = await _remediationService.GetRemediationLogsAsync(clientId);
            return Ok(logs);
        }
    }
}
