using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TerminationController : ControllerBase
    {
        private readonly TerminationService _terminationService;

        public TerminationController(
            TerminationService terminationService)
        {
            _terminationService = terminationService;
        }

        // POST: api/termination/terminate
        [HttpPost("terminate")]
        public async Task<IActionResult> Terminate(
            [FromBody] EndSessionRequest request)
        {
            EndSessionResponse response =
                await _terminationService.TerminateAsync(
                    request.SessionId,
                    request.Reason,
                    "Admin"
                );

            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        // POST: api/termination/auto
        [HttpPost("auto")]
        public async Task<IActionResult> AutoTerminate()
        {
            await _terminationService
                .AutoTerminateExpiredSessionsAsync();

            return Ok(new { message = "Auto termination check completed." });
        }
    }
}
