using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class EventService
    {
        private readonly string _connectionString;
        private readonly ILogger<EventService> _logger;

        public EventService(string connectionString, ILogger<EventService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
            EnsureTablesCreated();
        }

        private void EnsureTablesCreated()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ActivityEvents')
                BEGIN
                    CREATE TABLE ActivityEvents (
                        EventId       BIGINT IDENTITY(1,1) PRIMARY KEY,
                        EventType     NVARCHAR(50)   NOT NULL,
                        EventName     NVARCHAR(100)  NOT NULL,
                        Severity      NVARCHAR(20)   NOT NULL DEFAULT 'Info',
                        TimestampUtc  DATETIME       NOT NULL DEFAULT GETUTCDATE(),
                        ClientId      NVARCHAR(100)  NULL,
                        UserId        INT            NULL,
                        SessionId     INT            NULL,
                        Source        NVARCHAR(50)   NOT NULL DEFAULT 'Client',
                        Message       NVARCHAR(1000) NOT NULL,
                        MetadataJson  NVARCHAR(MAX)  NULL,
                        CorrelationId NVARCHAR(64)   NOT NULL DEFAULT NEWID(),
                        IPAddress     NVARCHAR(50)   NULL
                    );

                    CREATE INDEX IX_ActivityEvents_ClientId ON ActivityEvents(ClientId);
                    CREATE INDEX IX_ActivityEvents_TimestampUtc ON ActivityEvents(TimestampUtc);
                    CREATE INDEX IX_ActivityEvents_Severity ON ActivityEvents(Severity);
                END";

                using var cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify/create ActivityEvents table automatically.");
            }
        }

        public async Task<bool> IngestEventAsync(ActivityEventDto dto)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                INSERT INTO ActivityEvents 
                (EventType, EventName, Severity, TimestampUtc, ClientId, UserId, SessionId, Source, Message, MetadataJson, CorrelationId, IPAddress)
                VALUES 
                (@EventType, @EventName, @Severity, @TimestampUtc, @ClientId, @UserId, @SessionId, @Source, @Message, @MetadataJson, @CorrelationId, @IPAddress)";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@EventType", dto.EventType ?? "General");
                cmd.Parameters.AddWithValue("@EventName", dto.EventName ?? dto.EventType ?? "Event");
                cmd.Parameters.AddWithValue("@Severity", dto.Severity ?? "Info");
                cmd.Parameters.AddWithValue("@TimestampUtc", dto.TimestampUtc == default ? DateTime.UtcNow : dto.TimestampUtc);
                cmd.Parameters.AddWithValue("@ClientId", (object?)dto.ClientId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserId", (object?)dto.UserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SessionId", (object?)dto.SessionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Source", dto.Source ?? "Client");
                cmd.Parameters.AddWithValue("@Message", dto.Message ?? string.Empty);
                cmd.Parameters.AddWithValue("@MetadataJson", (object?)dto.MetadataJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CorrelationId", string.IsNullOrWhiteSpace(dto.CorrelationId) ? Guid.NewGuid().ToString("N") : dto.CorrelationId);
                cmd.Parameters.AddWithValue("@IPAddress", (object?)dto.IPAddress ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest activity event: {EventType}", dto.EventType);
                return false;
            }
        }

        public async Task<int> IngestBatchEventsAsync(List<ActivityEventDto> events)
        {
            if (events == null || events.Count == 0) return 0;
            int count = 0;
            foreach (var evt in events)
            {
                if (await IngestEventAsync(evt))
                    count++;
            }
            return count;
        }

        public async Task<EventTimelineResponse> GetTimelineAsync(EventFilterRequest req)
        {
            var response = new EventTimelineResponse
            {
                Page = req.Page > 0 ? req.Page : 1,
                PageSize = req.PageSize > 0 ? req.PageSize : 50
            };

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                var conditions = new List<string>();
                using var countCmd = new SqlCommand();
                countCmd.Connection = conn;

                if (!string.IsNullOrWhiteSpace(req.ClientId))
                {
                    conditions.Add("ClientId = @ClientId");
                    countCmd.Parameters.AddWithValue("@ClientId", req.ClientId);
                }
                if (req.UserId.HasValue)
                {
                    conditions.Add("UserId = @UserId");
                    countCmd.Parameters.AddWithValue("@UserId", req.UserId.Value);
                }
                if (req.SessionId.HasValue)
                {
                    conditions.Add("SessionId = @SessionId");
                    countCmd.Parameters.AddWithValue("@SessionId", req.SessionId.Value);
                }
                if (!string.IsNullOrWhiteSpace(req.EventType))
                {
                    conditions.Add("EventType = @EventType");
                    countCmd.Parameters.AddWithValue("@EventType", req.EventType);
                }
                if (!string.IsNullOrWhiteSpace(req.Severity))
                {
                    conditions.Add("Severity = @Severity");
                    countCmd.Parameters.AddWithValue("@Severity", req.Severity);
                }
                if (!string.IsNullOrWhiteSpace(req.Source))
                {
                    conditions.Add("Source = @Source");
                    countCmd.Parameters.AddWithValue("@Source", req.Source);
                }
                if (!string.IsNullOrWhiteSpace(req.SearchKeyword))
                {
                    conditions.Add("(Message LIKE @Keyword OR EventName LIKE @Keyword)");
                    countCmd.Parameters.AddWithValue("@Keyword", $"%{req.SearchKeyword}%");
                }
                if (req.DateFrom.HasValue)
                {
                    conditions.Add("TimestampUtc >= @DateFrom");
                    countCmd.Parameters.AddWithValue("@DateFrom", req.DateFrom.Value);
                }
                if (req.DateTo.HasValue)
                {
                    conditions.Add("TimestampUtc <= @DateTo");
                    countCmd.Parameters.AddWithValue("@DateTo", req.DateTo.Value);
                }

                string whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

                countCmd.CommandText = $"SELECT COUNT(*) FROM ActivityEvents {whereClause}";
                object? totalObj = await countCmd.ExecuteScalarAsync();
                response.TotalCount = Convert.ToInt32(totalObj ?? 0);

                int offset = (response.Page - 1) * response.PageSize;

                string querySql = $@"
                SELECT EventId, EventType, EventName, Severity, TimestampUtc, ClientId, UserId, SessionId, Source, Message, MetadataJson, CorrelationId, IPAddress
                FROM ActivityEvents
                {whereClause}
                ORDER BY TimestampUtc DESC
                OFFSET {offset} ROWS FETCH NEXT {response.PageSize} ROWS ONLY";

                using var queryCmd = new SqlCommand(querySql, conn);
                foreach (SqlParameter p in countCmd.Parameters)
                {
                    queryCmd.Parameters.Add((SqlParameter)((ICloneable)p).Clone());
                }

                using var reader = await queryCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    response.Events.Add(new ActivityEventDto
                    {
                        EventId = reader.GetInt64(0),
                        EventType = reader.GetString(1),
                        EventName = reader.GetString(2),
                        Severity = reader.GetString(3),
                        TimestampUtc = reader.GetDateTime(4),
                        ClientId = reader.IsDBNull(5) ? null : reader.GetString(5),
                        UserId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                        SessionId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        Source = reader.GetString(8),
                        Message = reader.GetString(9),
                        MetadataJson = reader.IsDBNull(10) ? null : reader.GetString(10),
                        CorrelationId = reader.GetString(11),
                        IPAddress = reader.IsDBNull(12) ? null : reader.GetString(12)
                    });
                }

                response.Success = true;
                response.Message = $"Retrieved {response.Events.Count} events.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve event timeline.");
                response.Success = false;
                response.Message = ex.Message;
            }

            return response;
        }
    }
}
