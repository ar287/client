using System;

namespace SessionManagement.Shared.DTOs
{
    public class PendingApprovalDto
    {
        public int Id { get; set; }
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string? ActionTarget { get; set; }
        public int RiskScore { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewedBy { get; set; }
        public string? ReviewNotes { get; set; }
    }

    public class ApprovalDecisionDto
    {
        public string ReviewedBy { get; set; } = "Admin";
        public string? ReviewNotes { get; set; }
    }
}
