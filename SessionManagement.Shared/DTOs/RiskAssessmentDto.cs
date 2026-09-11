using System;
using System.Collections.Generic;

namespace SessionManagement.Shared.DTOs
{
    public class RiskAssessmentDto
    {
        public long AssessmentId { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public int RiskScore { get; set; } // 0-100
        public string RiskLevel { get; set; } = "Low"; // Low (0-29), Medium (30-59), High (60-79), Critical (80-100)
        public List<string> Reasons { get; set; } = new();
        public List<long> EvidenceEventIds { get; set; } = new();
        public string RecommendedAction { get; set; } = "Monitor session activity.";
        public string EvaluatedBy { get; set; } = "RuleEngine"; // RuleEngine, OllamaAI, FallbackRule
        public double ConfidenceScore { get; set; } = 1.0;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
