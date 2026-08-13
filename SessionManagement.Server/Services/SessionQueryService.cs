using Microsoft.Data.SqlClient;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class SessionQueryService
    {
        private readonly string _connectionString;
        private const decimal   RatePerMinute = 2.00m;

        public SessionQueryService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<ActiveSessionsResponse>
            GetActiveSessionsAsync()
        {
            try
            {
                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT
                        s.SessionId,
                        s.UserId,
                        u.FullName,
                        u.Username,
                        s.AllocatedMinutes,
                        s.RemainingMinutes,
                        s.StartTime,
                        s.Status,
                        ISNULL(s.ClientMachine, 'Unknown')
                            AS ClientMachine,
                        s.ImagePath
                    FROM   Sessions s
                    INNER JOIN Users u ON s.UserId = u.UserId
                    WHERE  s.Status = 'Active'
                    ORDER  BY s.StartTime DESC";

                using var command =
                    new SqlCommand(query, connection);

                var sessions = new List<SessionDetailDto>();

                using var reader =
                    await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    DateTime startTime = reader.GetDateTime(6);
                    int elapsed = (int)Math.Floor(
                        (DateTime.Now - startTime).TotalMinutes);
                    int allocated  = reader.GetInt32(4);
                    int remaining  = Math.Max(0,
                        allocated - elapsed);
                    decimal cost   = elapsed * RatePerMinute;

                    sessions.Add(new SessionDetailDto
                    {
                        SessionId        = reader.GetInt32(0),
                        UserId           = reader.GetInt32(1),
                        FullName         = reader.GetString(2),
                        Username         = reader.GetString(3),
                        AllocatedMinutes = allocated,
                        RemainingMinutes = remaining,
                        StartTime        = startTime
                                            .ToString("hh:mm:ss tt"),
                        Status           = reader.GetString(7),
                        ClientMachine    = reader.GetString(8),
                        ImagePath        = reader.IsDBNull(9)
                                            ? null
                                            : reader.GetString(9),
                        RatePerMinute    = RatePerMinute,
                        CurrentCost      = cost,
                        ElapsedMinutes   = elapsed
                    });
                }

                return new ActiveSessionsResponse
                {
                    Success  = true,
                    Message  = $"{sessions.Count} active session(s).",
                    Sessions = sessions
                };
            }
            catch (Exception ex)
            {
                return new ActiveSessionsResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<SessionDetailDto?> GetSessionDetailAsync(
            int sessionId)
        {
            try
            {
                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT
                        s.SessionId,
                        s.UserId,
                        u.FullName,
                        u.Username,
                        s.AllocatedMinutes,
                        s.RemainingMinutes,
                        s.StartTime,
                        s.Status,
                        ISNULL(s.ClientMachine, 'Unknown'),
                        s.ImagePath
                    FROM   Sessions s
                    INNER JOIN Users u ON s.UserId = u.UserId
                    WHERE  s.SessionId = @SessionId";

                using var command =
                    new SqlCommand(query, connection);
                command.Parameters.AddWithValue(
                    "@SessionId", sessionId);

                using var reader =
                    await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync()) return null;

                DateTime startTime = reader.GetDateTime(6);
                int elapsed = (int)Math.Floor(
                    (DateTime.Now - startTime).TotalMinutes);
                int allocated = reader.GetInt32(4);
                decimal cost  = elapsed * RatePerMinute;

                return new SessionDetailDto
                {
                    SessionId        = reader.GetInt32(0),
                    UserId           = reader.GetInt32(1),
                    FullName         = reader.GetString(2),
                    Username         = reader.GetString(3),
                    AllocatedMinutes = allocated,
                    RemainingMinutes = Math.Max(0,
                        allocated - elapsed),
                    StartTime        = startTime
                                        .ToString("hh:mm:ss tt"),
                    Status           = reader.GetString(7),
                    ClientMachine    = reader.GetString(8),
                    ImagePath        = reader.IsDBNull(9)
                                        ? null
                                        : reader.GetString(9),
                    RatePerMinute    = RatePerMinute,
                    CurrentCost      = cost,
                    ElapsedMinutes   = elapsed
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
