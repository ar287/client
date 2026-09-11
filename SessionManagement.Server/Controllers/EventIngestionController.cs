using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventIngestionController : ControllerBase
    {
        private readonly EventService _eventService;

        public EventIngestionController(EventService eventService)
        {
            _eventService = eventService;
        }

        // POST: api/eventingestion/ingest
        [HttpPost("ingest")]
        public async Task<IActionResult> IngestSingle([FromBody] ActivityEventDto dto)
        {
            if (dto == null) return BadRequest(new EventIngestionResponse { Success = false, Message = "Event payload cannot be null." });

            bool success = await _eventService.IngestEventAsync(dto);
            return Ok(new EventIngestionResponse
            {
                Success = success,
                IngestedCount = success ? 1 : 0,
                Message = success ? "Event ingested successfully." : "Failed to ingest event."
            });
        }

        // POST: api/eventingestion/batch
        [HttpPost("batch")]
        public async Task<IActionResult> IngestBatch([FromBody] EventIngestionRequest request)
        {
            if (request == null || request.Events == null || request.Events.Count == 0)
                return BadRequest(new EventIngestionResponse { Success = false, Message = "Batch payload is empty." });

            int count = await _eventService.IngestBatchEventsAsync(request.Events);
            return Ok(new EventIngestionResponse
            {
                Success = count > 0,
                IngestedCount = count,
                Message = $"Ingested {count} of {request.Events.Count} events."
            });
        }

        // POST: api/eventingestion/timeline
        [HttpPost("timeline")]
        public async Task<IActionResult> GetTimeline([FromBody] EventFilterRequest filter)
        {
            filter ??= new EventFilterRequest();
            var timeline = await _eventService.GetTimelineAsync(filter);
            return Ok(timeline);
        }

        // GET: api/eventingestion/pc/{clientId}
        [HttpGet("pc/{clientId}")]
        public async Task<IActionResult> GetPcTimeline(string clientId, [FromQuery] int limit = 50)
        {
            var filter = new EventFilterRequest
            {
                ClientId = clientId,
                PageSize = limit > 0 ? limit : 50
            };
            var timeline = await _eventService.GetTimelineAsync(filter);
            return Ok(timeline);
        }
    }
}
