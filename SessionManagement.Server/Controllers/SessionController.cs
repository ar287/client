using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : ControllerBase
    {
        private readonly SessionService _sessionService;
        private readonly LogService     _logService;

        public SessionController(
            SessionService sessionService,
            LogService     logService)
        {
            _sessionService = sessionService;
            _logService     = logService;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartSession(
            [FromBody] StartSessionRequest request)
        {
            string ip =
                HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            StartSessionResponse response =
                await _sessionService.StartSessionAsync(request);

            if (response.Success)
            {
                await _logService.WriteLogAsync(
                    request.UserId,
                    response.SessionId,
                    "SessionStart",
                    $"Session #{response.SessionId} started " +
                    $"for user #{request.UserId}. " +
                    $"Duration: {request.AllocatedMinutes} min. " +
                    $"Machine: {request.ClientMachine}",
                    ip);
            }

            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        [HttpPost("end")]
        public async Task<IActionResult> EndSession(
            [FromBody] EndSessionRequest request)
        {
            string ip =
                HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            EndSessionResponse response =
                await _sessionService.EndSessionAsync(request);

            if (response.Success)
            {
                await _logService.WriteLogAsync(
                    null,
                    request.SessionId,
                    "SessionEnd",
                    $"Session #{request.SessionId} ended. " +
                    $"Duration: {response.TotalMinutes} min. " +
                    $"Amount: Rs.{response.TotalAmount:F2}. " +
                    $"Reason: {request.Reason}",
                    ip);
            }

            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        [HttpPost("extend")]
        public async Task<IActionResult> ExtendSession([FromBody] ApproveExtensionRequest request)
        {
            bool success = await _sessionService.ExtendSessionAsync(request.SessionId, request.AdditionalMinutes);
            if (success)
            {
                return Ok(new { success = true, message = $"Session extended by {request.AdditionalMinutes} minutes." });
            }
            return BadRequest(new { success = false, message = "Could not extend session." });
        }
    }
}
