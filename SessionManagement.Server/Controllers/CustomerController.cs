using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : ControllerBase
    {
        private readonly CustomerService _customerService;

        public CustomerController(CustomerService customerService)
        {
            _customerService = customerService;
        }

        // GET: api/customer/all?search=john&status=Active
        [HttpGet("all")]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] string? status = null)
        {
            var response =
                await _customerService.GetAllCustomersAsync(
                    search, status);

            return response.Success ? Ok(response) : BadRequest(response);
        }

        // POST: api/customer/create
        [HttpPost("create")]
        public async Task<IActionResult> Create(
            [FromBody] CreateCustomerRequest request)
        {
            var response =
                await _customerService.CreateCustomerAsync(request);

            return response.Success ? Ok(response) : BadRequest(response);
        }

        // PUT: api/customer/update
        [HttpPut("update")]
        public async Task<IActionResult> Update(
            [FromBody] UpdateCustomerRequest request)
        {
            var response =
                await _customerService.UpdateCustomerAsync(request);

            return response.Success ? Ok(response) : BadRequest(response);
        }

        // PUT: api/customer/togglestatus/3
        [HttpPut("togglestatus/{userId}")]
        public async Task<IActionResult> ToggleStatus(int userId)
        {
            var response =
                await _customerService.ToggleStatusAsync(userId);

            return response.Success ? Ok(response) : BadRequest(response);
        }

        // DELETE: api/customer/delete/3
        [HttpDelete("delete/{userId}")]
        public async Task<IActionResult> Delete(int userId)
        {
            var response =
                await _customerService.DeleteCustomerAsync(userId);

            return response.Success ? Ok(response) : BadRequest(response);
        }
    }
}
