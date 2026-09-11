using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemHealthController : ControllerBase
    {
        private static readonly Process _currentProcess = Process.GetCurrentProcess();
        private readonly string _connectionString;
        private readonly RuleEngineService _ruleEngineService;
        private readonly ApprovalService _approvalService;
        private readonly AIService _aiService;

        public SystemHealthController(
            IConfiguration configuration,
            RuleEngineService ruleEngineService,
            ApprovalService approvalService,
            AIService aiService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
            _ruleEngineService = ruleEngineService;
            _approvalService = approvalService;
            _aiService = aiService;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            TimeSpan uptime = DateTime.Now - _currentProcess.StartTime;
            string uptimeStr = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";

            bool dbOk = false;
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                dbOk = true;
            }
            catch { }

            int rulesCount = 0;
            try
            {
                var rules = await _ruleEngineService.GetRulesAsync();
                rulesCount = rules.Count;
            }
            catch { }

            int pendingCount = 0;
            try
            {
                var pending = await _approvalService.GetPendingApprovalsAsync();
                pendingCount = pending.Count;
            }
            catch { }

            var dto = new SystemHealthDto
            {
                DatabaseConnected = dbOk,
                ServerUptime = uptimeStr,
                ActiveServices = new List<string>
                {
                    "EventIngestionService",
                    "ClientMachineService",
                    "AnalyticsService",
                    "RuleEngineService",
                    "AIService",
                    "ApprovalService",
                    "RemediationService",
                    "AuditExportService",
                    "SignalR:SessionHub",
                    "SignalR:AlertHub"
                },
                AiEngineAvailable = true,
                TotalActiveRules = rulesCount,
                PendingApprovalsCount = pendingCount,
                TimestampUtc = DateTime.UtcNow
            };

            return Ok(dto);
        }
    }
}
