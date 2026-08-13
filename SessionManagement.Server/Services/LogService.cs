using Microsoft.Data.SqlClient;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class LogService
    {
        private readonly string _connectionString;

        public LogService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ── WRITE a log entry ────────────────────────────────────────
        public async Task WriteLogAsync(
            int?   userId,
            int?   sessionId,
            string eventType,
            string description,
            string ipAddress = "localhost")
        {
            try
            {
                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    INSERT INTO Logs
                        (UserId, SessionId,
                         EventType, Description, IPAddress)
                    VALUES
                        (@UserId, @SessionId,
                         @EventType, @Description, @IPAddress)";

                using var command =
                    new SqlCommand(query, connection);

                command.Parameters.AddWithValue(
                    "@UserId",
                    userId.HasValue
                        ? (object)userId.Value
                        : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@SessionId",
                    sessionId.HasValue
                        ? (object)sessionId.Value
                        : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@EventType",   eventType);
                command.Parameters.AddWithValue(
                    "@Description", description);
                command.Parameters.AddWithValue(
                    "@IPAddress",   ipAddress);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[LogService] Failed to write log: {ex.Message}");
            }
        }

        // ── GET logs with filters ────────────────────────────────────
        public async Task<LogsResponse> GetLogsAsync(
            string? search    = null,
            string? eventType = null,
            string? username  = null,
            string? dateFrom  = null,
            string? dateTo    = null,
            int     limit     = 200)
        {
            try
            {
                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT TOP (@Limit)
                        l.LogId,
                        l.UserId,
                        l.SessionId,
                        ISNULL(u.Username, 'System') AS Username,
                        ISNULL(u.FullName, 'System')  AS FullName,
                        l.EventType,
                        l.Description,
                        ISNULL(l.IPAddress, '-')       AS IPAddress,
                        l.CreatedAt
                    FROM   Logs l
                    LEFT JOIN Users u ON l.UserId = u.UserId
                    WHERE  1 = 1";

                if (!string.IsNullOrWhiteSpace(search))
                    query +=
                        " AND (l.Description LIKE @Search " +
                        "OR l.EventType LIKE @Search " +
                        "OR u.Username LIKE @Search)";

                if (!string.IsNullOrWhiteSpace(eventType))
                    query += " AND l.EventType = @EventType";

                if (!string.IsNullOrWhiteSpace(username))
                    query += " AND u.Username LIKE @Username";

                if (!string.IsNullOrWhiteSpace(dateFrom))
                    query += " AND l.CreatedAt >= @DateFrom";

                if (!string.IsNullOrWhiteSpace(dateTo))
                    query += " AND l.CreatedAt <= @DateTo";

                query += " ORDER BY l.CreatedAt DESC";

                using var command =
                    new SqlCommand(query, connection);

                command.Parameters.AddWithValue("@Limit", limit);

                if (!string.IsNullOrWhiteSpace(search))
                    command.Parameters.AddWithValue(
                        "@Search", $"%{search}%");

                if (!string.IsNullOrWhiteSpace(eventType))
                    command.Parameters.AddWithValue(
                        "@EventType", eventType);

                if (!string.IsNullOrWhiteSpace(username))
                    command.Parameters.AddWithValue(
                        "@Username", $"%{username}%");

                if (!string.IsNullOrWhiteSpace(dateFrom) &&
                    DateTime.TryParse(dateFrom, out DateTime df))
                    command.Parameters.AddWithValue("@DateFrom", df);

                if (!string.IsNullOrWhiteSpace(dateTo) &&
                    DateTime.TryParse(dateTo, out DateTime dt))
                    command.Parameters.AddWithValue(
                        "@DateTo", dt.AddDays(1));

                var logs = new List<LogDto>();

                using var reader =
                    await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    logs.Add(new LogDto
                    {
                        LogId       = reader.GetInt32(0),
                        UserId      = reader.IsDBNull(1)
                                        ? null
                                        : reader.GetInt32(1),
                        SessionId   = reader.IsDBNull(2)
                                        ? null
                                        : reader.GetInt32(2),
                        Username    = reader.GetString(3),
                        FullName    = reader.GetString(4),
                        EventType   = reader.GetString(5),
                        Description = reader.GetString(6),
                        IPAddress   = reader.GetString(7),
                        CreatedAt   = reader.GetDateTime(8)
                                            .ToString(
                                            "dd MMM yyyy  HH:mm:ss")
                    });
                }

                return new LogsResponse
                {
                    Success    = true,
                    Message    = $"{logs.Count} log(s) found.",
                    Logs       = logs,
                    TotalCount = logs.Count
                };
            }
            catch (Exception ex)
            {
                return new LogsResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        // ── GET summary stats ────────────────────────────────────────
        public async Task<Dictionary<string, int>> GetLogStatsAsync()
        {
            var stats = new Dictionary<string, int>();

            try
            {
                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT EventType, COUNT(*) AS Count
                    FROM   Logs
                    GROUP  BY EventType
                    ORDER  BY Count DESC";

                using var command =
                    new SqlCommand(query, connection);

                using var reader =
                    await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    stats[reader.GetString(0)] = reader.GetInt32(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[LogService] Stats error: {ex.Message}");
            }

            return stats;
        }

        // ── CLEAR old logs (older than X days) ──────────────────────
        public async Task<int> ClearOldLogsAsync(int daysOld = 30)
        {
            using var connection =
                new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                DELETE FROM Logs
                WHERE CreatedAt < DATEADD(DAY, -@Days, GETDATE())";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Days", daysOld);

            return await command.ExecuteNonQueryAsync();
        }

        // ── EXPORT logs to CSV string ────────────────────────────────
        public async Task<string> ExportToCsvAsync(
            string? eventType = null)
        {
            var response = await GetLogsAsync(
                eventType: eventType, limit: 10000);

            if (!response.Success || response.Logs.Count == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                "LogId,UserId,SessionId,Username," +
                "FullName,EventType,Description,IPAddress,CreatedAt");

            foreach (var log in response.Logs)
            {
                sb.AppendLine(
                    $"{log.LogId}," +
                    $"{log.UserId?.ToString() ?? ""}," +
                    $"{log.SessionId?.ToString() ?? ""}," +
                    $"\"{log.Username}\"," +
                    $"\"{log.FullName}\"," +
                    $"{log.EventType}," +
                    $"\"{log.Description.Replace("\"", "'")}\"," +
                    $"{log.IPAddress}," +
                    $"{log.CreatedAt}");
            }

            return sb.ToString();
        }
    }
}
