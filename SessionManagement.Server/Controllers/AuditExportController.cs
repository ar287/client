using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuditExportController : ControllerBase
    {
        private readonly AuditExportService _auditExportService;

        public AuditExportController(AuditExportService auditExportService)
        {
            _auditExportService = auditExportService;
        }

        [HttpPost("timeline")]
        public async Task<IActionResult> GetTimeline([FromBody] AuditExportFilterDto filter)
        {
            filter ??= new AuditExportFilterDto();
            var timeline = await _auditExportService.GetForensicTimelineAsync(filter);
            return Ok(timeline);
        }

        [HttpPost("export")]
        public async Task<IActionResult> ExportAuditLog([FromBody] AuditExportFilterDto filter)
        {
            filter ??= new AuditExportFilterDto();
            var result = await _auditExportService.GenerateExportFileAsync(filter);
            return Ok(result);
        }
    }
}
