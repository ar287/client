using System;
using System.Collections.Generic;

namespace SessionManagement.Shared.DTOs
{
    public class AuditExportFilterDto
    {
        public DateTime? StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }
        public string? ClientId { get; set; }
        public string? SourceCategory { get; set; } // All, ActivityEvent, RiskAssessment, PendingApproval, RemediationLog
        public string MinSeverity { get; set; } = "Low"; // Low, Medium, High, Critical
        public string Format { get; set; } = "CSV"; // CSV, JSON
    }

    public class ForensicRecordDto
    {
        public DateTime TimestampUtc { get; set; }
        public string SourceCategory { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string EventTypeOrRule { get; set; } = string.Empty;
        public string Severity { get; set; } = "Low";
        public string Summary { get; set; } = string.Empty;
        public string? DetailsJson { get; set; }
    }

    public class AuditExportResultDto
    {
        public string ExportId { get; set; } = Guid.NewGuid().ToString();
        public string Format { get; set; } = "CSV";
        public int RecordCount { get; set; }
        public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
        public string ContentString { get; set; } = string.Empty;
        public string SuggestedFileName { get; set; } = string.Empty;
    }
}
