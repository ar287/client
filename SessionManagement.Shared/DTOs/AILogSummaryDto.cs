using System.Collections.Generic;

namespace SessionManagement.Shared.DTOs
{
    public class AILogSummaryDto
    {
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyEvents { get; set; } = new List<string>();
        public string OperationalRisk { get; set; } = "Low";
        public string Timestamp { get; set; } = string.Empty;
        public bool IsFallback { get; set; }
    }
}
