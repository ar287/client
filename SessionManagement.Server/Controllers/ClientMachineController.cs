using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientMachineController : ControllerBase
    {
        private readonly ClientMachineService _machineService;
        private readonly EventService _eventService;

        public ClientMachineController(ClientMachineService machineService, EventService eventService)
        {
            _machineService = machineService;
            _eventService = eventService;
        }

        // POST: api/clientmachine/heartbeat
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ClientId))
            {
                return BadRequest(new HeartbeatResponse { Success = false, Message = "ClientId is required." });
            }

            var machine = await _machineService.RegisterOrUpdateMachineAsync(request);

            // Ingest Heartbeat activity event
            await _eventService.IngestEventAsync(new ActivityEventDto
            {
                EventType = "Heartbeat",
                EventName = "Client Heartbeat Received",
                Severity = "Info",
                ClientId = request.ClientId,
                UserId = request.CurrentUserId,
                SessionId = request.CurrentSessionId,
                Source = "Client",
                Message = $"Heartbeat from {request.ClientId} ({request.IPAddress ?? "unknown IP"})",
                IPAddress = request.IPAddress
            });

            return Ok(new HeartbeatResponse
            {
                Success = true,
                Status = machine.Status,
                ServerTimeUtc = System.DateTime.UtcNow,
                Message = "Heartbeat recorded successfully."
            });
        }

        // GET: api/clientmachine/all
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var machines = await _machineService.GetAllMachinesAsync();
            return Ok(machines);
        }

        // GET: api/clientmachine/{clientId}
        [HttpGet("{clientId}")]
        public async Task<IActionResult> GetByClientId(string clientId)
        {
            var machine = await _machineService.GetMachineByClientIdAsync(clientId);
            if (machine == null)
                return NotFound(new { message = $"Client Machine {clientId} not found." });

            return Ok(machine);
        }
    }
}
