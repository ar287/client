using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AnalyticsService _analyticsService;

        public AnalyticsController(AnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        // GET: api/analytics/overview
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            var summary = await _analyticsService.GetAnalyticsSummaryAsync(dateFrom, dateTo);
            return Ok(summary);
        }

        // GET: api/analytics/pc-ranking
        [HttpGet("pc-ranking")]
        public async Task<IActionResult> GetPcRanking(
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            var ranking = await _analyticsService.GetPcRankingAsync(dateFrom, dateTo);
            return Ok(ranking);
        }
    }
}
