using System.Collections.Generic;

namespace SessionManagement.Shared.DTOs
{
    public class EventIngestionRequest
    {
        public List<ActivityEventDto> Events { get; set; } = new();
    }

    public class EventIngestionResponse
    {
        public bool Success { get; set; }
        public int IngestedCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
