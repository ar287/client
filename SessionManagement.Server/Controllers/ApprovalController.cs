using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApprovalController : ControllerBase
    {
        private readonly ApprovalService _approvalService;

        public ApprovalController(ApprovalService approvalService)
        {
            _approvalService = approvalService;
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingApprovals()
        {
            var list = await _approvalService.GetPendingApprovalsAsync();
            return Ok(list);
        }

        [HttpPost("queue")]
        public async Task<IActionResult> QueueApproval([FromBody] PendingApprovalDto request)
        {
            if (request == null) return BadRequest("Invalid request.");

            int id = await _approvalService.QueueApprovalAsync(request);
            if (id > 0) return Ok(new { success = true, approvalId = id });

            return StatusCode(500, "Failed to queue approval request.");
        }

        [HttpPost("{id:int}/approve")]
        public async Task<IActionResult> ApproveAction(int id, [FromBody] ApprovalDecisionDto decision)
        {
            bool result = await _approvalService.ProcessApprovalAsync(
                id,
                "Approved",
                decision?.ReviewedBy ?? "Admin",
                decision?.ReviewNotes ?? "Approved by administrator.");

            if (result) return Ok(new { success = true, message = "Approval action authorized." });
            return BadRequest("Approval item not found or already processed.");
        }

        [HttpPost("{id:int}/reject")]
        public async Task<IActionResult> RejectAction(int id, [FromBody] ApprovalDecisionDto decision)
        {
            bool result = await _approvalService.ProcessApprovalAsync(
                id,
                "Rejected",
                decision?.ReviewedBy ?? "Admin",
                decision?.ReviewNotes ?? "Rejected by administrator.");

            if (result) return Ok(new { success = true, message = "Approval action rejected." });
            return BadRequest("Approval item not found or already processed.");
        }
    }
}
