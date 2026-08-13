using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using SessionManagement.Server.Hubs;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class TerminationService
    {
        private readonly string                    _connectionString;
        private readonly IHubContext<SessionHub>   _hubContext;
        private const decimal                      RatePerMinute = 2.00m;

        public TerminationService(
            string connectionString,
            IHubContext<SessionHub> hubContext)
        {
            _connectionString = connectionString;
            _hubContext        = hubContext;
        }

        // Terminate a session — called by admin or auto expiry
        public async Task<EndSessionResponse> TerminateAsync(
            int sessionId,
            string reason,
            string terminatedBy)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Fetch session details
                string selectQuery = @"
                    SELECT StartTime, AllocatedMinutes, UserId, Status
                    FROM   Sessions
                    WHERE  SessionId = @SessionId";

                using var selectCmd = new SqlCommand(selectQuery, connection);
                selectCmd.Parameters.AddWithValue("@SessionId", sessionId);

                DateTime startTime;
                int      allocatedMinutes;
                int      userId;
                string   currentStatus;

                using var reader = await selectCmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return new EndSessionResponse
                    {
                        Success = false,
                        Message = "Session not found."
                    };
                }

                startTime        = reader.GetDateTime(0);
                allocatedMinutes = reader.GetInt32(1);
                userId           = reader.GetInt32(2);
                currentStatus    = reader.GetString(3);
                reader.Close();

                // Prevent double termination
                if (currentStatus != "Active")
                {
                    return new EndSessionResponse
                    {
                        Success = false,
                        Message = $"Session is already {currentStatus}."
                    };
                }

                // Calculate actual usage
                int totalMinutes = (int)Math.Ceiling(
                    (DateTime.Now - startTime).TotalMinutes);
                totalMinutes = Math.Min(totalMinutes, allocatedMinutes);

                decimal totalAmount = totalMinutes * RatePerMinute;

                // Map reason to valid DB status
                string dbStatus = reason switch
                {
                    "Terminated" => "Terminated",
                    "Completed"  => "Completed",
                    _            => "Terminated"
                };

                // Update Sessions table
                string updateSession = @"
                    UPDATE Sessions
                    SET    Status           = @Status,
                           EndTime          = GETDATE(),
                           RemainingMinutes = 0
                    WHERE  SessionId        = @SessionId";

                using var updateCmd =
                    new SqlCommand(updateSession, connection);
                updateCmd.Parameters.AddWithValue("@Status",    dbStatus);
                updateCmd.Parameters.AddWithValue("@SessionId", sessionId);
                await updateCmd.ExecuteNonQueryAsync();

                // Update Billing table
                string updateBilling = @"
                    UPDATE Billing
                    SET    TotalMinutes = @TotalMinutes,
                           TotalAmount  = @TotalAmount
                    WHERE  SessionId    = @SessionId";

                using var billingCmd =
                    new SqlCommand(updateBilling, connection);
                billingCmd.Parameters.AddWithValue(
                    "@TotalMinutes", totalMinutes);
                billingCmd.Parameters.AddWithValue(
                    "@TotalAmount",  totalAmount);
                billingCmd.Parameters.AddWithValue(
                    "@SessionId",    sessionId);
                await billingCmd.ExecuteNonQueryAsync();

                // Log termination event
                string insertLog = @"
                    INSERT INTO Logs
                        (UserId, SessionId, EventType, Description)
                    VALUES
                        (@UserId, @SessionId, @EventType, @Description)";

                using var logCmd = new SqlCommand(insertLog, connection);
                logCmd.Parameters.AddWithValue("@UserId",    userId);
                logCmd.Parameters.AddWithValue("@SessionId", sessionId);
                logCmd.Parameters.AddWithValue("@EventType", "SessionTerminated");
                logCmd.Parameters.AddWithValue(
                    "@Description",
                    $"Session terminated by {terminatedBy}. " +
                    $"Reason: {reason}. " +
                    $"Duration: {totalMinutes} min. " +
                    $"Amount: Rs.{totalAmount:F2}"
                );
                await logCmd.ExecuteNonQueryAsync();

                // Insert Alert
                string insertAlert = @"
                    INSERT INTO Alerts
                        (UserId, AlertType, Description, Severity)
                    VALUES
                        (@UserId, @AlertType, @Description, @Severity)";

                using var alertCmd = new SqlCommand(insertAlert, connection);
                alertCmd.Parameters.AddWithValue("@UserId",    userId);
                alertCmd.Parameters.AddWithValue(
                    "@AlertType",   "SessionTerminated");
                alertCmd.Parameters.AddWithValue(
                    "@Description",
                    $"Session #{sessionId} terminated by {terminatedBy}. " +
                    $"Reason: {reason}"
                );
                alertCmd.Parameters.AddWithValue("@Severity", "High");
                await alertCmd.ExecuteNonQueryAsync();

                // Notify the customer via SignalR
                string customerGroup = $"Customer_{userId}";
                await _hubContext.Clients
                    .Group(customerGroup)
                    .SendAsync("SessionTerminated", reason);

                // Notify all admins via SignalR
                await _hubContext.Clients
                    .Group("Admins")
                    .SendAsync(
                        "SessionEnded",
                        userId, sessionId,
                        totalMinutes, totalAmount
                    );

                return new EndSessionResponse
                {
                    Success      = true,
                    Message      = $"Session terminated successfully.",
                    TotalMinutes = totalMinutes,
                    TotalAmount  = totalAmount
                };
            }
            catch (Exception ex)
            {
                return new EndSessionResponse
                {
                    Success = false,
                    Message = $"Termination error: {ex.Message}"
                };
            }
        }

        // Check and auto-terminate all sessions whose time has expired
        // This runs as a background check every 60 seconds on the server
        public async Task AutoTerminateExpiredSessionsAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Find all sessions that are active but time has expired
                string selectQuery = @"
                    SELECT SessionId, UserId
                    FROM   Sessions
                    WHERE  Status = 'Active'
                    AND    DATEADD(MINUTE, AllocatedMinutes, StartTime)
                           < GETDATE()";

                using var selectCmd = new SqlCommand(selectQuery, connection);
                var expiredSessions = new List<(int SessionId, int UserId)>();

                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    expiredSessions.Add((
                        reader.GetInt32(0),
                        reader.GetInt32(1)
                    ));
                }
                reader.Close();

                // Terminate each expired session
                foreach (var (sessionId, userId) in expiredSessions)
                {
                    Console.WriteLine(
                        $"[AutoTerminate] Terminating expired session " +
                        $"#{sessionId} for user #{userId}");

                    await TerminateAsync(
                        sessionId,
                        "Completed",
                        "System (Auto)"
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[AutoTerminate] Error: {ex.Message}");
            }
        }
    }
}
