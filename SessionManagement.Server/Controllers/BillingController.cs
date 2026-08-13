using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BillingController : ControllerBase
    {
        private readonly BillingService _billingService;

        public BillingController(BillingService billingService)
        {
            _billingService = billingService;
        }

        // GET: api/billing/user/2
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserBilling(int userId)
        {
            var response = await _billingService.GetUserBillingAsync(userId);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        // GET: api/billing/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAllBilling()
        {
            var response = await _billingService.GetAllBillingAsync();
            return response.Success ? Ok(response) : BadRequest(response);
        }

        // PUT: api/billing/markpaid/5
        [HttpPut("markpaid/{billingId}")]
        public async Task<IActionResult> MarkAsPaid(int billingId)
        {
            bool success = await _billingService.MarkAsPaidAsync(billingId);
            return success
                ? Ok(new { message = "Marked as paid." })
                : BadRequest(new { message = "Failed to update." });
        }
    }
}
