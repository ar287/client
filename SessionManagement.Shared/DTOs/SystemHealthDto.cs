using System;
using System.Collections.Generic;

namespace SessionManagement.Shared.DTOs
{
    public class SystemHealthDto
    {
        public bool DatabaseConnected { get; set; }
        public string ServerUptime { get; set; } = string.Empty;
        public List<string> ActiveServices { get; set; } = new List<string>();
        public bool AiEngineAvailable { get; set; }
        public int TotalActiveRules { get; set; }
        public int PendingApprovalsCount { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
