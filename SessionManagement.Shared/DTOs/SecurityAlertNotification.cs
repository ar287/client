using System;

namespace SessionManagement.Shared.DTOs
{
    /// <summary>
    /// Typed real-time alert payload pushed from server to Admin via SignalR
    /// when a security rule fires or a manual security event is raised.
    /// </summary>
    public class SecurityAlertNotification
    {
        public string AlertId       { get; set; } = Guid.NewGuid().ToString();
        public string ClientId      { get; set; } = string.Empty;
        public string ClientName    { get; set; } = string.Empty;
        public string RuleName      { get; set; } = string.Empty;
        public string AlertType     { get; set; } = string.Empty;  // RuleFired, ManualAlert, AiFlag
        public string Severity      { get; set; } = "Medium";       // Low, Medium, High, Critical
        public string Message       { get; set; } = string.Empty;
        public string ActionTaken   { get; set; } = string.Empty;  // CreateAlert, NotifyAdmin, etc.
        public bool   RequiresApproval { get; set; } = false;
        public int    RiskScore     { get; set; } = 0;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
