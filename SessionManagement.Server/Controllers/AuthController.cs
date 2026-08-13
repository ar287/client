using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly LogService  _logService;

        public AuthController(
            AuthService authService,
            LogService  logService)
        {
            _authService = authService;
            _logService  = logService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            string ip =
                HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            LoginResponse response =
                await _authService.LoginAsync(request, ip);

            if (response.Success)
            {
                await _logService.WriteLogAsync(
                    response.UserId,
                    null,
                    "Login",
                    $"User '{request.Username}' logged in " +
                    $"successfully from {ip}.",
                    ip);
            }
            else
            {
                await _logService.WriteLogAsync(
                    null,
                    null,
                    "LoginFailed",
                    $"Failed login attempt for " +
                    $"'{request.Username}' from {ip}. " +
                    $"Reason: {response.Message}",
                    ip);
            }

            return response.Success
                ? Ok(response)
                : Unauthorized(response);
        }
    }
}
