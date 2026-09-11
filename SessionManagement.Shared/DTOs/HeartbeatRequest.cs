using System;

namespace SessionManagement.Shared.DTOs
{
    public class HeartbeatRequest
    {
        public string ClientId { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string? IPAddress { get; set; }
        public string? MacAddress { get; set; }
        public string? OSVersion { get; set; }
        public int? CurrentUserId { get; set; }
        public int? CurrentSessionId { get; set; }
        public string Status { get; set; } = "Online";
    }

    public class HeartbeatResponse
    {
        public bool Success { get; set; }
        public string Status { get; set; } = "Online";
        public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
        public bool CommandPending { get; set; } = false;
    }
}
