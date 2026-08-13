using Microsoft.Data.SqlClient;
using SessionManagement.Shared.DTOs;
using SessionManagement.Shared.Models;

namespace SessionManagement.Server.Services
{
    public class SessionService
    {
        private readonly string _connectionString;
        private const decimal RatePerMinute = 2.00m;

        public SessionService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Start a new session
        public async Task<StartSessionResponse> StartSessionAsync(
            StartSessionRequest request)
        {
            try
            {
                // Check if user already has an active session
                bool hasActive = await HasActiveSessionAsync(request.UserId);
                if (hasActive)
                {
                    return new StartSessionResponse
                    {
                        Success = false,
                        Message = "You already have an active session."
                    };
                }

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Insert new session
                string insertQuery = @"
                    INSERT INTO Sessions
                        (UserId, AllocatedMinutes, RemainingMinutes,
                         Status, ClientMachine, StartTime)
                    OUTPUT INSERTED.SessionId, INSERTED.StartTime
                    VALUES
                        (@UserId, @AllocatedMinutes, @AllocatedMinutes,
                         'Active', @ClientMachine, GETDATE())";

                using var command = new SqlCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@UserId",
                    request.UserId);
                command.Parameters.AddWithValue("@AllocatedMinutes",
                    request.AllocatedMinutes);
                command.Parameters.AddWithValue("@ClientMachine",
                    request.ClientMachine);

                int sessionId;
                DateTime startTime;

                using var reader = await command.ExecuteReaderAsync();
                await reader.ReadAsync();
                sessionId = reader.GetInt32(0);
                startTime = reader.GetDateTime(1);
                reader.Close();

                // Create billing record
                await CreateBillingRecordAsync(
                    connection, sessionId, request.UserId);

                // Log the session start
                await LogEventAsync(
                    connection,
                    request.UserId,
                    sessionId,
                    "SessionStart",
                    $"Session started for {request.AllocatedMinutes} minutes."
                );

                return new StartSessionResponse
                {
                    Success          = true,
                    Message          = "Session started successfully.",
                    SessionId        = sessionId,
                    AllocatedMinutes = request.AllocatedMinutes,
                    StartTime        = startTime
                };
            }
            catch (Exception ex)
            {
                return new StartSessionResponse
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}"
                };
            }
        }

        // End a session and calculate billing
        public async Task<EndSessionResponse> EndSessionAsync(
            EndSessionRequest request)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Get session start time
                string selectQuery = @"
                    SELECT StartTime, AllocatedMinutes, UserId
                    FROM   Sessions
                    WHERE  SessionId = @SessionId
                    AND    Status    = 'Active'";

                using var selectCmd = new SqlCommand(selectQuery, connection);
                selectCmd.Parameters.AddWithValue(
                    "@SessionId", request.SessionId);

                DateTime startTime;
                int allocatedMinutes;
                int userId;

                using var reader = await selectCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new EndSessionResponse
                    {
                        Success = false,
                        Message = "Active session not found."
                    };
                }

                startTime        = reader.GetDateTime(0);
                allocatedMinutes = reader.GetInt32(1);
                userId           = reader.GetInt32(2);
                reader.Close();

                // Calculate actual minutes used
                int totalMinutes = (int)Math.Ceiling(
                    (DateTime.Now - startTime).TotalMinutes
                );

                // Never charge more than allocated
                totalMinutes = Math.Min(totalMinutes, allocatedMinutes);

                decimal totalAmount = totalMinutes * RatePerMinute;

                // Update session record
                string updateSession = @"
                    UPDATE Sessions
                    SET    Status    = @Status,
                           EndTime   = GETDATE(),
                           RemainingMinutes = 0
                    WHERE  SessionId = @SessionId";

                using var updateCmd = new SqlCommand(updateSession, connection);
                updateCmd.Parameters.AddWithValue("@Status", request.Reason);
                updateCmd.Parameters.AddWithValue(
                    "@SessionId", request.SessionId);
                await updateCmd.ExecuteNonQueryAsync();

                // Update billing record
                string updateBilling = @"
                    UPDATE Billing
                    SET    TotalMinutes = @TotalMinutes,
                           TotalAmount  = @TotalAmount
                    WHERE  SessionId   = @SessionId";

                using var billingCmd = new SqlCommand(updateBilling, connection);
                billingCmd.Parameters.AddWithValue(
                    "@TotalMinutes", totalMinutes);
                billingCmd.Parameters.AddWithValue(
                    "@TotalAmount",  totalAmount);
                billingCmd.Parameters.AddWithValue(
                    "@SessionId",    request.SessionId);
                await billingCmd.ExecuteNonQueryAsync();

                // Log session end
                await LogEventAsync(
                    connection,
                    userId,
                    request.SessionId,
                    "SessionEnd",
                    $"Session ended. Duration: {totalMinutes} min. " +
                    $"Amount: Rs.{totalAmount}"
                );

                return new EndSessionResponse
                {
                    Success      = true,
                    Message      = "Session ended successfully.",
                    TotalMinutes = totalMinutes,
                    TotalAmount  = totalAmount
                };
            }
            catch (Exception ex)
            {
                return new EndSessionResponse
                {
                    Success = false,
                    Message = $"Server error: {ex.Message}"
                };
            }
        }

        // Check if user already has an active session
        private async Task<bool> HasActiveSessionAsync(int userId)
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

            int count = (int)await command.ExecuteScalarAsync()!;
            return count > 0;
        }

        // Create billing record when session starts
        private async Task CreateBillingRecordAsync(
            SqlConnection connection, int sessionId, int userId)
        {
            string query = @"
                INSERT INTO Billing
                    (SessionId, UserId, RatePerMinute, TotalMinutes, TotalAmount)
                VALUES
                    (@SessionId, @UserId, @RatePerMinute, 0, 0)";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@SessionId",     sessionId);
            command.Parameters.AddWithValue("@UserId",        userId);
            command.Parameters.AddWithValue("@RatePerMinute", RatePerMinute);
            await command.ExecuteNonQueryAsync();
        }

        // Log any event to Logs table
        private async Task LogEventAsync(
            SqlConnection connection,
            int userId, int sessionId,
            string eventType, string description)
        {
            string query = @"
                INSERT INTO Logs
                    (UserId, SessionId, EventType, Description)
                VALUES
                    (@UserId, @SessionId, @EventType, @Description)";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserId",      userId);
            command.Parameters.AddWithValue("@SessionId",   sessionId);
            command.Parameters.AddWithValue("@EventType",   eventType);
            command.Parameters.AddWithValue("@Description", description);
            await command.ExecuteNonQueryAsync();
        }
    }
}
