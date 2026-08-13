using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogController : ControllerBase
    {
        private readonly LogService _logService;
        private readonly AIService _aiService;

        public LogController(LogService logService, AIService aiService)
        {
            _logService = logService;
            _aiService = aiService;
        }

        // POST: api/log/ai-summary
        [HttpPost("ai-summary")]
        public async Task<IActionResult> GenerateAISummary([FromQuery] int limit = 50)
        {
            var logsResp = await _logService.GetLogsAsync(null, null, null, null, null, limit);
            if (!logsResp.Success || logsResp.Logs == null)
                return BadRequest(new { message = "Failed to retrieve logs for summary." });

            var summary = await _aiService.GenerateLogSummaryAsync(logsResp.Logs);
            return Ok(summary);
        }

        // POST: api/log/ai-parse-query
        [HttpPost("ai-parse-query")]
        public async Task<IActionResult> ParseNaturalLanguageQuery([FromBody] AILogFilterRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UserQuery))
                return BadRequest(new { message = "Query string cannot be empty." });

            var parsedResult = await _aiService.ParseNaturalLanguageLogQueryAsync(request.UserQuery);
            return Ok(parsedResult);
        }

        // GET: api/log/all
        [HttpGet("all")]
        public async Task<IActionResult> GetLogs(
            [FromQuery] string? search    = null,
            [FromQuery] string? eventType = null,
            [FromQuery] string? username  = null,
            [FromQuery] string? dateFrom  = null,
            [FromQuery] string? dateTo    = null,
            [FromQuery] int     limit     = 200)
        {
            var response = await _logService.GetLogsAsync(
                search, eventType, username,
                dateFrom, dateTo, limit);

            return response.Success
                ? Ok(response)
                : BadRequest(response);
        }

        // GET: api/log/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _logService.GetLogStatsAsync();
            return Ok(stats);
        }

        // POST: api/log/write
        [HttpPost("write")]
        public async Task<IActionResult> WriteLog(
            [FromBody] LogDto log)
        {
            string ip =
                HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            await _logService.WriteLogAsync(
                log.UserId,
                log.SessionId,
                log.EventType,
                log.Description,
                ip);

            return Ok(new { message = "Log written." });
        }

        // DELETE: api/log/clear?daysOld=30
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearOldLogs(
            [FromQuery] int daysOld = 30)
        {
            int deleted =
                await _logService.ClearOldLogsAsync(daysOld);

            return Ok(new
            {
                message = $"Deleted {deleted} old log(s).",
                deleted
            });
        }

        // GET: api/log/export
        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] string? eventType = null)
        {
            string csv =
                await _logService.ExportToCsvAsync(eventType);

            if (string.IsNullOrEmpty(csv))
                return BadRequest(
                    new { message = "No logs to export." });

            byte[] bytes =
                System.Text.Encoding.UTF8.GetBytes(csv);

            return File(
                bytes,
                "text/csv",
                $"logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
    }
}
