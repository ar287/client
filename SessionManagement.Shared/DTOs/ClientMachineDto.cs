using System;

namespace SessionManagement.Shared.DTOs
{
    public class ClientMachineDto
    {
        public int MachineId { get; set; }
        public string ClientId { get; set; } = string.Empty; // e.g. LAB-PC-01
        public string MachineName { get; set; } = string.Empty;
        public string? IPAddress { get; set; }
        public string? MacAddress { get; set; }
        public string? OSVersion { get; set; }
        public string Status { get; set; } = "Offline"; // Online, Offline, InUse, Locked, UnderReview
        public DateTime? LastHeartbeatUtc { get; set; }
        public int? CurrentUserId { get; set; }
        public string? CurrentUsername { get; set; }
        public int? CurrentSessionId { get; set; }
        public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsOnline => LastHeartbeatUtc.HasValue && (DateTime.UtcNow - LastHeartbeatUtc.Value).TotalSeconds <= 60;
    }
}
