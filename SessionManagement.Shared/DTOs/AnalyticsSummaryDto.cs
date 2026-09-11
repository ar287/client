using System;
using System.Collections.Generic;

namespace SessionManagement.Shared.DTOs
{
    public class AnalyticsSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalSessions { get; set; }
        public int TotalUsageMinutes { get; set; }
        public double AverageSessionMinutes { get; set; }
        public int ActiveSessionsCount { get; set; }
        public int TotalRegisteredPcs { get; set; }
        public int OnlinePcsCount { get; set; }
        public double OccupancyPercentage { get; set; }
        public string MostUsedPc { get; set; } = "N/A";
        public string MostProfitablePc { get; set; } = "N/A";
        public int PeakHour { get; set; } = 14; // Default 2 PM peak hour if no data
        public string PeakHourFormatted => $"{PeakHour:D2}:00 - {(PeakHour + 1) % 24:D2}:00";
        public List<PcUsageMetricDto> TopPcsByUsage { get; set; } = new();
        public List<PcUsageMetricDto> TopPcsByRevenue { get; set; } = new();
        public List<HourlyMetricDto> HourlyBreakdown { get; set; } = new();
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class PcUsageMetricDto
    {
        public string ClientId { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public int TotalMinutes { get; set; }
        public decimal TotalRevenue { get; set; }
        public double UtilizationPercentage { get; set; }
    }

    public class HourlyMetricDto
    {
        public int Hour { get; set; }
        public string HourLabel => $"{Hour:D2}:00";
        public int ActiveSessions { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalMinutes { get; set; }
    }
}
