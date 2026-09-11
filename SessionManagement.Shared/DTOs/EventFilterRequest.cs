using System;
using System.Collections.Generic;

namespace SessionManagement.Shared.DTOs
{
    public class EventFilterRequest
    {
        public string? ClientId { get; set; }
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public int? SessionId { get; set; }
        public string? EventType { get; set; }
        public string? Severity { get; set; }
        public string? Source { get; set; }
        public string? SearchKeyword { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class EventTimelineResponse
    {
        public bool Success { get; set; }
        public List<ActivityEventDto> Events { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
