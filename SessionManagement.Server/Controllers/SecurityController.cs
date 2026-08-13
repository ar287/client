using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SecurityController : ControllerBase
    {
        private readonly SecurityService _securityService;
        private readonly LogService _logService;
        private readonly AIService _aiService;

        public SecurityController(SecurityService securityService, LogService logService, AIService aiService)
        {
            _securityService = securityService;
            _logService = logService;
            _aiService = aiService;
        }

        // POST: api/security/alerts/{alertId}/ai-analyze
        [HttpPost("alerts/{alertId}/ai-analyze")]
        public async Task<IActionResult> AnalyzeAlertWithAI(int alertId)
        {
            var alertsResp = await _securityService.GetFilteredAlertsAsync(null, null, false, 200);
            if (!alertsResp.Success || alertsResp.Alerts == null)
                return NotFound(new { message = "Alert list unavailable." });

            var alert = alertsResp.Alerts.FirstOrDefault(a => a.AlertId == alertId);
            if (alert == null)
                return NotFound(new { message = $"Alert ID {alertId} not found." });

            var logsResp = await _logService.GetLogsAsync(username: alert.Username, limit: 20);
            var logs = logsResp.Success && logsResp.Logs != null ? logsResp.Logs : new List<LogDto>();
            var analyzedAlert = await _aiService.AnalyzeSecurityAlertAsync(alert, logs);

            return Ok(analyzedAlert);
        }

        // GET: api/security/alerts
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts(
            [FromQuery] int    limit      = 100,
            [FromQuery] string? severity  = null,
            [FromQuery] string? alertType = null,
            [FromQuery] bool   unreadOnly = false)
        {
            var response = await _securityService
                .GetFilteredAlertsAsync(
                    severity, alertType, unreadOnly, limit);

            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        // PUT: api/security/markread
        [HttpPut("markread")]
        public async Task<IActionResult> MarkAllRead()
        {
            await _securityService.MarkAllAlertsReadAsync();
            return Ok(new { message = "All alerts marked as read." });
        }

        // PUT: api/security/markread/5
        [HttpPut("markread/{alertId}")]
        public async Task<IActionResult> MarkSingleRead(int alertId)
        {
            await _securityService.MarkSingleAlertReadAsync(alertId);
            return Ok(new { message = "Alert marked as read." });
        }
    }
}
