using System;

namespace SessionManagement.Shared.DTOs
{
    public class RemediationRequestDto
    {
        public string ClientId { get; set; } = string.Empty;
        public string ActionType { get; set; } = "LockScreen"; // LockScreen, TerminateSession, KillProcess, BlockAccess
        public string? TargetProcessName { get; set; }
        public string? Reason { get; set; }
        public string InitiatedBy { get; set; } = "Admin";
        public int RiskScore { get; set; } = 0;
    }

    public class RemediationLogDto
    {
        public int Id { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string? TargetProcessName { get; set; }
        public string? Reason { get; set; }
        public string InitiatedBy { get; set; } = string.Empty;
        public int RiskScore { get; set; }
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
