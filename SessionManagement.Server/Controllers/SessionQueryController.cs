using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionQueryController : ControllerBase
    {
        private readonly SessionQueryService _queryService;

        public SessionQueryController(
            SessionQueryService queryService)
        {
            _queryService = queryService;
        }

        // GET: api/sessionquery/active
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveSessions()
        {
            var response =
                await _queryService.GetActiveSessionsAsync();

            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        // GET: api/sessionquery/detail/3
        [HttpGet("detail/{sessionId}")]
        public async Task<IActionResult> GetDetail(int sessionId)
        {
            var detail =
                await _queryService.GetSessionDetailAsync(
                    sessionId);

            return detail != null
                ? Ok(detail)
                : NotFound(new { message = "Session not found." });
        }
    }
}
