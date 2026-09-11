using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class AnalyticsService
    {
        private readonly string _connectionString;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(string connectionString, ILogger<AnalyticsService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<AnalyticsSummaryDto> GetAnalyticsSummaryAsync(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var summary = new AnalyticsSummaryDto();
            var from = dateFrom ?? DateTime.Today.AddDays(-30);
            var to = dateTo ?? DateTime.UtcNow.AddDays(1);

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // 1. Overall Billing & Sessions metrics
                string sqlSummary = @"
                SELECT 
                    ISNULL(SUM(b.TotalAmount), 0.00) AS TotalRevenue,
                    COUNT(s.SessionId) AS TotalSessions,
                    ISNULL(SUM(b.TotalMinutes), 0) AS TotalUsageMinutes,
                    ISNULL(AVG(CAST(b.TotalMinutes AS FLOAT)), 0.0) AS AvgSessionMinutes
                FROM Sessions s
                LEFT JOIN Billing b ON s.SessionId = b.SessionId
                WHERE s.StartTime >= @DateFrom AND s.StartTime <= @DateTo";

                using (var cmd = new SqlCommand(sqlSummary, conn))
                {
                    cmd.Parameters.AddWithValue("@DateFrom", from);
                    cmd.Parameters.AddWithValue("@DateTo", to);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        summary.TotalRevenue = reader.GetDecimal(0);
                        summary.TotalSessions = reader.GetInt32(1);
                        summary.TotalUsageMinutes = reader.GetInt32(2);
                        summary.AverageSessionMinutes = Math.Round(reader.GetDouble(3), 1);
                    }
                }

                // 2. PC counts & occupancy
                string sqlPcCounts = @"
                SELECT 
                    (SELECT COUNT(*) FROM ClientMachines) AS TotalPcs,
                    (SELECT COUNT(*) FROM ClientMachines WHERE Status != 'Offline' AND DATEDIFF(SECOND, ISNULL(LastHeartbeatUtc, '2000-01-01'), GETUTCDATE()) <= 60) AS OnlinePcs,
                    (SELECT COUNT(*) FROM Sessions WHERE Status = 'Active') AS ActiveSessions";

                using (var cmd = new SqlCommand(sqlPcCounts, conn))
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        summary.TotalRegisteredPcs = reader.GetInt32(0);
                        summary.OnlinePcsCount = reader.GetInt32(1);
                        summary.ActiveSessionsCount = reader.GetInt32(2);
                        if (summary.TotalRegisteredPcs > 0)
                        {
                            summary.OccupancyPercentage = Math.Round(((double)summary.ActiveSessionsCount / summary.TotalRegisteredPcs) * 100.0, 1);
                        }
                    }
                }

                // 3. Top PCs by usage and revenue
                summary.TopPcsByUsage = await GetPcRankingInternalAsync(conn, from, to, "TotalMinutes DESC");
                summary.TopPcsByRevenue = await GetPcRankingInternalAsync(conn, from, to, "TotalRevenue DESC");

                if (summary.TopPcsByUsage.Count > 0)
                    summary.MostUsedPc = summary.TopPcsByUsage[0].ClientId;

                if (summary.TopPcsByRevenue.Count > 0)
                    summary.MostProfitablePc = summary.TopPcsByRevenue[0].ClientId;

                // 4. Hourly breakdown & peak hour
                summary.HourlyBreakdown = await GetHourlyBreakdownInternalAsync(conn, from, to);
                if (summary.HourlyBreakdown.Count > 0)
                {
                    int peakH = 14;
                    int maxSessions = -1;
                    foreach (var h in summary.HourlyBreakdown)
                    {
                        if (h.ActiveSessions > maxSessions)
                        {
                            maxSessions = h.ActiveSessions;
                            peakH = h.Hour;
                        }
                    }
                    summary.PeakHour = peakH;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compute analytics summary.");
            }

            return summary;
        }

        public async Task<List<PcUsageMetricDto>> GetPcRankingAsync(DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            var from = dateFrom ?? DateTime.Today.AddDays(-30);
            var to = dateTo ?? DateTime.UtcNow.AddDays(1);

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            return await GetPcRankingInternalAsync(conn, from, to, "TotalRevenue DESC");
        }

        private async Task<List<PcUsageMetricDto>> GetPcRankingInternalAsync(SqlConnection conn, DateTime from, DateTime to, string orderBy)
        {
            var list = new List<PcUsageMetricDto>();
            string sql = $@"
            SELECT 
                ISNULL(s.ClientMachine, 'Unassigned') AS ClientId,
                COUNT(s.SessionId) AS SessionCount,
                ISNULL(SUM(b.TotalMinutes), 0) AS TotalMinutes,
                ISNULL(SUM(b.TotalAmount), 0.00) AS TotalRevenue
            FROM Sessions s
            LEFT JOIN Billing b ON s.SessionId = b.SessionId
            WHERE s.StartTime >= @DateFrom AND s.StartTime <= @DateTo
            GROUP BY s.ClientMachine
            ORDER BY {orderBy}";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DateFrom", from);
            cmd.Parameters.AddWithValue("@DateTo", to);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PcUsageMetricDto
                {
                    ClientId = reader.GetString(0),
                    SessionCount = reader.GetInt32(1),
                    TotalMinutes = reader.GetInt32(2),
                    TotalRevenue = reader.GetDecimal(3),
                    UtilizationPercentage = Math.Min(100.0, Math.Round((reader.GetInt32(2) / (24.0 * 60.0)) * 100.0, 1))
                });
            }

            return list;
        }

        private async Task<List<HourlyMetricDto>> GetHourlyBreakdownInternalAsync(SqlConnection conn, DateTime from, DateTime to)
        {
            var dict = new Dictionary<int, HourlyMetricDto>();
            for (int i = 0; i < 24; i++)
            {
                dict[i] = new HourlyMetricDto { Hour = i, ActiveSessions = 0, TotalRevenue = 0, TotalMinutes = 0 };
            }

            string sql = @"
            SELECT 
                DATEPART(HOUR, s.StartTime) AS SessionHour,
                COUNT(s.SessionId) AS SessionCount,
                ISNULL(SUM(b.TotalAmount), 0.00) AS TotalRevenue,
                ISNULL(SUM(b.TotalMinutes), 0) AS TotalMinutes
            FROM Sessions s
            LEFT JOIN Billing b ON s.SessionId = b.SessionId
            WHERE s.StartTime >= @DateFrom AND s.StartTime <= @DateTo
            GROUP BY DATEPART(HOUR, s.StartTime)";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DateFrom", from);
            cmd.Parameters.AddWithValue("@DateTo", to);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int hour = reader.GetInt32(0);
                if (dict.ContainsKey(hour))
                {
                    dict[hour].ActiveSessions = reader.GetInt32(1);
                    dict[hour].TotalRevenue = reader.GetDecimal(2);
                    dict[hour].TotalMinutes = reader.GetInt32(3);
                }
            }

            return new List<HourlyMetricDto>(dict.Values);
        }
    }
}
