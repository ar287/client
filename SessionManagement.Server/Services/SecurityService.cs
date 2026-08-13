using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using SessionManagement.Server.Hubs;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class SecurityService
    {
        private readonly string                  _connectionString;
        private readonly IHubContext<SessionHub> _hubContext;

        // In-memory tracking for failed attempts
        // Key: username, Value: (count, first attempt time)
        private readonly Dictionary<string, (int Count, DateTime FirstAttempt)>
            _failedAttempts = new();

        // In-memory tracking for request rates
        // Key: IP address, Value: (count, window start time)
        private readonly Dictionary<string, (int Count, DateTime WindowStart)>
            _requestCounts = new();

        private readonly object _lock = new();

        // Thresholds
        private const int    MaxFailedAttempts    = 3;
        private const int    MaxRequestsPerMinute = 30;
        private const double LockoutMinutes       = 15;

        public SecurityService(
            string connectionString,
            IHubContext<SessionHub> hubContext)
        {
            _connectionString = connectionString;
            _hubContext        = hubContext;
        }

        // ── Failed Login Detection ───────────────────────────────────

        public async Task<(bool IsBlocked, string Message)>
            CheckFailedLoginAsync(string username, string ipAddress)
        {
            lock (_lock)
            {
                if (_failedAttempts.TryGetValue(
                    username, out var attempts))
                {
                    // Reset counter if lockout window has passed
                    if ((DateTime.Now - attempts.FirstAttempt).TotalMinutes
                        > LockoutMinutes)
                    {
                        _failedAttempts.Remove(username);
                        return (false, string.Empty);
                    }

                    // Block if too many attempts
                    if (attempts.Count >= MaxFailedAttempts)
                    {
                        double remaining = LockoutMinutes -
                            (DateTime.Now - attempts.FirstAttempt)
                            .TotalMinutes;

                        return (
                            true,
                            $"Account temporarily locked. " +
                            $"Try again in {(int)remaining} minutes."
                        );
                    }
                }
            }

            return (false, string.Empty);
        }

        public async Task RecordFailedLoginAsync(
            string username, string ipAddress)
        {
            lock (_lock)
            {
                if (_failedAttempts.TryGetValue(
                    username, out var attempts))
                {
                    _failedAttempts[username] =
                        (attempts.Count + 1, attempts.FirstAttempt);
                }
                else
                {
                    _failedAttempts[username] = (1, DateTime.Now);
                }
            }

            int count = _failedAttempts.ContainsKey(username)
                ? _failedAttempts[username].Count
                : 1;

            // Determine severity based on attempt count
            string severity = count >= MaxFailedAttempts ? "High" :
                              count >= 2               ? "Medium" : "Low";

            string description =
                $"Failed login attempt #{count} for username " +
                $"'{username}' from IP {ipAddress} at {DateTime.Now:HH:mm:ss}";

            // Log to DB
            await InsertAlertAsync(
                null,
                "FailedLogin",
                description,
                severity
            );

            // Notify admin in real time if threshold reached
            if (count >= MaxFailedAttempts)
            {
                await NotifyAdminAsync(
                    "FailedLogin",
                    $"SECURITY: Account '{username}' locked after " +
                    $"{count} failed attempts from {ipAddress}",
                    severity
                );
            }
        }

        public void ClearFailedAttempts(string username)
        {
            lock (_lock)
            {
                _failedAttempts.Remove(username);
            }
        }

        // ── Duplicate Session Detection ──────────────────────────────

        public async Task<bool> HasActiveSessionAsync(int userId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT COUNT(*)
                FROM   Sessions
                WHERE  UserId = @UserId
                AND    Status = 'Active'";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserId", userId);

            int count = (int)(await command.ExecuteScalarAsync())!;
            return count > 0;
        }

        public async Task RecordDuplicateSessionAttemptAsync(
            int userId, string username)
        {
            string description =
                $"User '{username}' (ID: {userId}) attempted to start " +
                $"a duplicate session while one is already active. " +
                $"Time: {DateTime.Now:HH:mm:ss}";

            await InsertAlertAsync(
                userId,
                "DuplicateSession",
                description,
                "Medium"
            );

            await NotifyAdminAsync(
                "DuplicateSession",
                $"SECURITY: '{username}' attempted duplicate session.",
                "Medium"
            );
        }

        // ── Rate Limiting Detection ──────────────────────────────────

        public async Task<bool> IsRateLimitedAsync(string ipAddress)
        {
            lock (_lock)
            {
                if (_requestCounts.TryGetValue(
                    ipAddress, out var rate))
                {
                    // Reset window every minute
                    if ((DateTime.Now - rate.WindowStart).TotalMinutes >= 1)
                    {
                        _requestCounts[ipAddress] = (1, DateTime.Now);
                        return false;
                    }

                    if (rate.Count >= MaxRequestsPerMinute)
                        return true;

                    _requestCounts[ipAddress] =
                        (rate.Count + 1, rate.WindowStart);
                }
                else
                {
                    _requestCounts[ipAddress] = (1, DateTime.Now);
                }
            }

            return false;
        }

        public async Task RecordRateLimitViolationAsync(string ipAddress)
        {
            string description =
                $"Rate limit exceeded from IP {ipAddress}. " +
                $"More than {MaxRequestsPerMinute} requests/minute. " +
                $"Time: {DateTime.Now:HH:mm:ss}";

            await InsertAlertAsync(
                null,
                "RateLimit",
                description,
                "High"
            );

            await NotifyAdminAsync(
                "RateLimit",
                $"SECURITY: Rate limit exceeded from {ipAddress}",
                "High"
            );
        }

        // ── Suspicious Activity ──────────────────────────────────────

        public async Task RecordSuspiciousActivityAsync(
            int?   userId,
            string activityType,
            string description)
        {
            await InsertAlertAsync(
                userId,
                activityType,
                description,
                "High"
            );

            await NotifyAdminAsync(
                activityType,
                $"SECURITY: {description}",
                "High"
            );
        }

        // ── Get Alerts for Admin ─────────────────────────────────────

        public async Task<SecurityAlertsResponse> GetAlertsAsync(
            int limit = 50)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT TOP (@Limit)
                        a.AlertId,
                        a.UserId,
                        ISNULL(u.Username, 'Unknown') AS Username,
                        a.AlertType,
                        a.Description,
                        a.Severity,
                        a.IsRead,
                        a.CreatedAt
                    FROM   Alerts a
                    LEFT JOIN Users u ON a.UserId = u.UserId
                    ORDER BY a.CreatedAt DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Limit", limit);

                var alerts = new List<SecurityAlert>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    alerts.Add(new SecurityAlert
                    {
                        AlertId     = reader.GetInt32(0),
                        UserId      = reader.IsDBNull(1)
                                        ? null
                                        : reader.GetInt32(1),
                        Username    = reader.GetString(2),
                        AlertType   = reader.GetString(3),
                        Description = reader.GetString(4),
                        Severity    = reader.GetString(5),
                        IsRead      = reader.GetBoolean(6),
                        CreatedAt   = reader.GetDateTime(7)
                                            .ToString("dd MMM HH:mm:ss")
                    });
                }

                int unread = alerts.Count(a => !a.IsRead);

                return new SecurityAlertsResponse
                {
                    Success     = true,
                    Message     = $"{alerts.Count} alert(s) found.",
                    Alerts      = alerts,
                    UnreadCount = unread
                };
            }
            catch (Exception ex)
            {
                return new SecurityAlertsResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task MarkAllAlertsReadAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = "UPDATE Alerts SET IsRead = 1 WHERE IsRead = 0";
            using var command = new SqlCommand(query, connection);
            await command.ExecuteNonQueryAsync();
        }

        // ── Private Helpers ──────────────────────────────────────────

        private async Task InsertAlertAsync(
            int?   userId,
            string alertType,
            string description,
            string severity)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                INSERT INTO Alerts
                    (UserId, AlertType, Description, Severity)
                VALUES
                    (@UserId, @AlertType, @Description, @Severity)";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue(
                "@UserId",
                userId.HasValue ? (object)userId.Value : DBNull.Value
            );
            command.Parameters.AddWithValue("@AlertType",   alertType);
            command.Parameters.AddWithValue("@Description", description);
            command.Parameters.AddWithValue("@Severity",    severity);

            await command.ExecuteNonQueryAsync();
        }

        private async Task NotifyAdminAsync(
            string alertType,
            string message,
            string severity)
        {
            await _hubContext.Clients
                .Group("Admins")
                .SendAsync(
                    "SecurityAlert",
                    alertType, message, severity
                );
        }
        // Get alerts filtered by type or severity
        public async Task<SecurityAlertsResponse> GetFilteredAlertsAsync(
            string? severity  = null,
            string? alertType = null,
            bool    unreadOnly = false,
            int     limit      = 100)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT TOP (@Limit)
                        a.AlertId,
                        a.UserId,
                        ISNULL(u.Username, 'System') AS Username,
                        a.AlertType,
                        a.Description,
                        a.Severity,
                        a.IsRead,
                        a.CreatedAt
                    FROM   Alerts a
                    LEFT JOIN Users u ON a.UserId = u.UserId
                    WHERE  1 = 1";

                if (!string.IsNullOrWhiteSpace(severity))
                    query += " AND a.Severity = @Severity";

                if (!string.IsNullOrWhiteSpace(alertType))
                    query += " AND a.AlertType = @AlertType";

                if (unreadOnly)
                    query += " AND a.IsRead = 0";

                query += " ORDER BY a.CreatedAt DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Limit", limit);

                if (!string.IsNullOrWhiteSpace(severity))
                    command.Parameters.AddWithValue("@Severity", severity);

                if (!string.IsNullOrWhiteSpace(alertType))
                    command.Parameters.AddWithValue("@AlertType", alertType);

                var alerts = new List<SecurityAlert>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    alerts.Add(new SecurityAlert
                    {
                        AlertId     = reader.GetInt32(0),
                        UserId      = reader.IsDBNull(1)
                                        ? null
                                        : reader.GetInt32(1),
                        Username    = reader.GetString(2),
                        AlertType   = reader.GetString(3),
                        Description = reader.GetString(4),
                        Severity    = reader.GetString(5),
                        IsRead      = reader.GetBoolean(6),
                        CreatedAt   = reader.GetDateTime(7)
                                            .ToString("dd MMM yyyy  HH:mm:ss")
                    });
                }

                return new SecurityAlertsResponse
                {
                    Success     = true,
                    Message     = $"{alerts.Count} alert(s) found.",
                    Alerts      = alerts,
                    UnreadCount = alerts.Count(a => !a.IsRead)
                };
            }
            catch (Exception ex)
            {
                return new SecurityAlertsResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        // Mark a single alert as read
        public async Task MarkSingleAlertReadAsync(int alertId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query =
                "UPDATE Alerts SET IsRead = 1 WHERE AlertId = @AlertId";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@AlertId", alertId);
            await command.ExecuteNonQueryAsync();
        }
    }
}
