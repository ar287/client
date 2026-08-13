using Microsoft.Data.SqlClient;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class BillingService
    {
        private readonly string _connectionString;

        public BillingService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Get billing history for a specific customer
        public async Task<BillingResponse> GetUserBillingAsync(int userId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT
                        b.BillingId,
                        b.SessionId,
                        b.UserId,
                        u.FullName,
                        b.RatePerMinute,
                        b.TotalMinutes,
                        b.TotalAmount,
                        b.IsPaid,
                        b.GeneratedAt,
                        s.Status
                    FROM   Billing b
                    INNER JOIN Users    u ON b.UserId    = u.UserId
                    INNER JOIN Sessions s ON b.SessionId = s.SessionId
                    WHERE  b.UserId = @UserId
                    ORDER  BY b.GeneratedAt DESC";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserId", userId);

                var records = new List<BillingRecord>();
                decimal total = 0;

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var record = new BillingRecord
                    {
                        BillingId     = reader.GetInt32(0),
                        SessionId     = reader.GetInt32(1),
                        UserId        = reader.GetInt32(2),
                        FullName      = reader.GetString(3),
                        RatePerMinute = reader.GetDecimal(4),
                        TotalMinutes  = reader.GetInt32(5),
                        TotalAmount   = reader.GetDecimal(6),
                        IsPaid        = reader.GetBoolean(7),
                        GeneratedAt   = reader.GetDateTime(8)
                                              .ToString("dd MMM yyyy  hh:mm tt"),
                        SessionStatus = reader.GetString(9)
                    };

                    records.Add(record);
                    total += record.TotalAmount;
                }

                return new BillingResponse
                {
                    Success = true,
                    Message = $"{records.Count} record(s) found.",
                    Records = records,
                    Total   = total
                };
            }
            catch (Exception ex)
            {
                return new BillingResponse
                {
                    Success = false,
                    Message = $"Error fetching billing: {ex.Message}"
                };
            }
        }

        // Get all billing records for admin
        public async Task<BillingResponse> GetAllBillingAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT
                        b.BillingId,
                        b.SessionId,
                        b.UserId,
                        u.FullName,
                        b.RatePerMinute,
                        b.TotalMinutes,
                        b.TotalAmount,
                        b.IsPaid,
                        b.GeneratedAt,
                        s.Status
                    FROM   Billing b
                    INNER JOIN Users    u ON b.UserId    = u.UserId
                    INNER JOIN Sessions s ON b.SessionId = s.SessionId
                    ORDER  BY b.GeneratedAt DESC";

                using var command = new SqlCommand(query, connection);

                var records = new List<BillingRecord>();
                decimal total = 0;

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var record = new BillingRecord
                    {
                        BillingId     = reader.GetInt32(0),
                        SessionId     = reader.GetInt32(1),
                        UserId        = reader.GetInt32(2),
                        FullName      = reader.GetString(3),
                        RatePerMinute = reader.GetDecimal(4),
                        TotalMinutes  = reader.GetInt32(5),
                        TotalAmount   = reader.GetDecimal(6),
                        IsPaid        = reader.GetBoolean(7),
                        GeneratedAt   = reader.GetDateTime(8)
                                              .ToString("dd MMM yyyy  hh:mm tt"),
                        SessionStatus = reader.GetString(9)
                    };

                    records.Add(record);
                    total += record.TotalAmount;
                }

                return new BillingResponse
                {
                    Success = true,
                    Message = $"{records.Count} record(s) found.",
                    Records = records,
                    Total   = total
                };
            }
            catch (Exception ex)
            {
                return new BillingResponse
                {
                    Success = false,
                    Message = $"Error fetching billing: {ex.Message}"
                };
            }
        }

        // Mark a billing record as paid
        public async Task<bool> MarkAsPaidAsync(int billingId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    UPDATE Billing
                    SET    IsPaid = 1
                    WHERE  BillingId = @BillingId";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@BillingId", billingId);

                int rows = await command.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
