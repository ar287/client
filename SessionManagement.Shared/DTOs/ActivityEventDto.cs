using System;

namespace SessionManagement.Shared.DTOs
{
    public class ActivityEventDto
    {
        public long EventId { get; set; }
        public string EventType { get; set; } = string.Empty; // e.g. SessionStart, FailedLogin, ProcessBlocked
        public string EventName { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info"; // Info, Low, Medium, High, Critical
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string? ClientId { get; set; }
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public int? SessionId { get; set; }
        public string Source { get; set; } = "Client"; // Client, Server, Admin
        public string Message { get; set; } = string.Empty;
        public string? MetadataJson { get; set; }
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
        public string? IPAddress { get; set; }
    }
}
