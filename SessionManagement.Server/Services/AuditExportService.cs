using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class AuditExportService
    {
        private readonly string _connectionString;
        private readonly ILogger<AuditExportService> _logger;

        public AuditExportService(string connectionString, ILogger<AuditExportService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<List<ForensicRecordDto>> GetForensicTimelineAsync(AuditExportFilterDto filter)
        {
            var records = new List<ForensicRecordDto>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                DateTime fromDate = filter.StartDateUtc ?? DateTime.UtcNow.AddDays(-30);
                DateTime toDate = filter.EndDateUtc ?? DateTime.UtcNow.AddDays(1);

                // 1. Query ActivityEvents
                if (string.IsNullOrWhiteSpace(filter.SourceCategory) || filter.SourceCategory.Equals("All", StringComparison.OrdinalIgnoreCase) || filter.SourceCategory.Equals("ActivityEvent", StringComparison.OrdinalIgnoreCase))
                {
                    string sqlEvents = @"
                    SELECT TimestampUtc, ClientId, EventType, Severity, Message, DetailsJson
                    FROM ActivityEvents
                    WHERE TimestampUtc >= @FromDate AND TimestampUtc <= @ToDate";

                    if (!string.IsNullOrWhiteSpace(filter.ClientId)) sqlEvents += " AND ClientId = @ClientId";

                    using var cmd = new SqlCommand(sqlEvents, conn);
                    cmd.Parameters.AddWithValue("@FromDate", fromDate);
                    cmd.Parameters.AddWithValue("@ToDate", toDate);
                    if (!string.IsNullOrWhiteSpace(filter.ClientId)) cmd.Parameters.AddWithValue("@ClientId", filter.ClientId);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        records.Add(new ForensicRecordDto
                        {
                            TimestampUtc = reader.GetDateTime(0),
                            SourceCategory = "ActivityEvent",
                            ClientId = reader.GetString(1),
                            EventTypeOrRule = reader.GetString(2),
                            Severity = reader.GetString(3),
                            Summary = reader.GetString(4),
                            DetailsJson = reader.IsDBNull(5) ? null : reader.GetString(5)
                        });
                    }
                }

                // 2. Query RemediationLogs if table exists
                if (string.IsNullOrWhiteSpace(filter.SourceCategory) || filter.SourceCategory.Equals("All", StringComparison.OrdinalIgnoreCase) || filter.SourceCategory.Equals("RemediationLog", StringComparison.OrdinalIgnoreCase))
                {
                    string sqlRemediation = @"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'RemediationLogs')
                    BEGIN
                        SELECT ExecutedAtUtc, ClientId, ActionType, InitiatedBy, Message, Reason
                        FROM RemediationLogs
                        WHERE ExecutedAtUtc >= @FromDate AND ExecutedAtUtc <= @ToDate
                    END";

                    using var cmdR = new SqlCommand(sqlRemediation, conn);
                    cmdR.Parameters.AddWithValue("@FromDate", fromDate);
                    cmdR.Parameters.AddWithValue("@ToDate", toDate);

                    using var readerR = await cmdR.ExecuteReaderAsync();
                    while (await readerR.ReadAsync())
                    {
                        records.Add(new ForensicRecordDto
                        {
                            TimestampUtc = readerR.GetDateTime(0),
                            SourceCategory = "Remediation",
                            ClientId = readerR.GetString(1),
                            EventTypeOrRule = readerR.GetString(2),
                            Severity = "High",
                            Summary = readerR.IsDBNull(4) ? "Remediation executed" : readerR.GetString(4),
                            DetailsJson = readerR.IsDBNull(5) ? null : readerR.GetString(5)
                        });
                    }
                }

                // 3. Query PendingApprovals if table exists
                if (string.IsNullOrWhiteSpace(filter.SourceCategory) || filter.SourceCategory.Equals("All", StringComparison.OrdinalIgnoreCase) || filter.SourceCategory.Equals("PendingApproval", StringComparison.OrdinalIgnoreCase))
                {
                    string sqlApprovals = @"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'PendingApprovals')
                    BEGIN
                        SELECT RequestedAtUtc, ClientId, RuleName, Status, Reason, ReviewNotes
                        FROM PendingApprovals
                        WHERE RequestedAtUtc >= @FromDate AND RequestedAtUtc <= @ToDate
                    END";

                    using var cmdA = new SqlCommand(sqlApprovals, conn);
                    cmdA.Parameters.AddWithValue("@FromDate", fromDate);
                    cmdA.Parameters.AddWithValue("@ToDate", toDate);

                    using var readerA = await cmdA.ExecuteReaderAsync();
                    while (await readerA.ReadAsync())
                    {
                        records.Add(new ForensicRecordDto
                        {
                            TimestampUtc = readerA.GetDateTime(0),
                            SourceCategory = "ApprovalAction",
                            ClientId = readerA.GetString(1),
                            EventTypeOrRule = readerA.GetString(2),
                            Severity = "Medium",
                            Summary = $"Action status: {readerA.GetString(3)}",
                            DetailsJson = readerA.IsDBNull(4) ? null : readerA.GetString(4)
                        });
                    }
                }

                // Sort timeline by timestamp descending
                records.Sort((a, b) => b.TimestampUtc.CompareTo(a.TimestampUtc));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build forensic timeline export.");
            }

            return records;
        }

        public async Task<AuditExportResultDto> GenerateExportFileAsync(AuditExportFilterDto filter)
        {
            var records = await GetForensicTimelineAsync(filter);
            string timestampStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

            var result = new AuditExportResultDto
            {
                Format = filter.Format,
                RecordCount = records.Count,
                ExportedAtUtc = DateTime.UtcNow
            };

            if (filter.Format.Equals("JSON", StringComparison.OrdinalIgnoreCase))
            {
                result.SuggestedFileName = $"ForensicAudit_{timestampStr}.json";
                result.ContentString = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                // CSV export
                result.SuggestedFileName = $"ForensicAudit_{timestampStr}.csv";
                var sb = new StringBuilder();
                sb.AppendLine("TimestampUtc,SourceCategory,ClientId,EventTypeOrRule,Severity,Summary,Details");

                foreach (var r in records)
                {
                    string safeSummary = CleanCsvField(r.Summary);
                    string safeDetails = CleanCsvField(r.DetailsJson ?? "");
                    sb.AppendLine($"{r.TimestampUtc:O},{r.SourceCategory},{r.ClientId},{r.EventTypeOrRule},{r.Severity},\"{safeSummary}\",\"{safeDetails}\"");
                }

                result.ContentString = sb.ToString();
            }

            return result;
        }

        private static string CleanCsvField(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
