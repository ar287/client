using Microsoft.Data.SqlClient;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class CustomerService
    {
        private readonly string _connectionString;

        public CustomerService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ── GET ALL CUSTOMERS ────────────────────────────────────────
        public async Task<CustomerListResponse> GetAllCustomersAsync(
            string? search    = null,
            string? statusFilter = null)
        {
            try
            {
                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT
                        u.UserId,
                        u.FullName,
                        u.Username,
                        u.IsActive,
                        u.CreatedAt,
                        COUNT(DISTINCT s.SessionId) AS TotalSessions,
                        ISNULL(SUM(b.TotalAmount), 0) AS TotalSpent,
                        MAX(s.StartTime) AS LastSeen
                    FROM Users u
                    LEFT JOIN Sessions s ON u.UserId = s.UserId
                    LEFT JOIN Billing  b ON u.UserId = b.UserId
                    WHERE u.Role = 'Customer'";

                // Dynamic filters
                if (!string.IsNullOrWhiteSpace(search))
                    query += @" AND (
                        u.FullName  LIKE @Search OR
                        u.Username  LIKE @Search)";

                if (statusFilter == "Active")
                    query += " AND u.IsActive = 1";
                else if (statusFilter == "Inactive")
                    query += " AND u.IsActive = 0";

                query += @"
                    GROUP BY
                        u.UserId, u.FullName,
                        u.Username, u.IsActive, u.CreatedAt
                    ORDER BY u.CreatedAt DESC";

                using var command = new SqlCommand(query, connection);

                if (!string.IsNullOrWhiteSpace(search))
                    command.Parameters.AddWithValue(
                        "@Search", $"%{search}%");

                var customers = new List<CustomerDto>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    customers.Add(new CustomerDto
                    {
                        UserId        = reader.GetInt32(0),
                        FullName      = reader.GetString(1),
                        Username      = reader.GetString(2),
                        IsActive      = reader.GetBoolean(3),
                        CreatedAt     = reader.GetDateTime(4)
                                               .ToString("dd MMM yyyy"),
                        TotalSessions = reader.GetInt32(5),
                        TotalSpent    = reader.GetDecimal(6),
                        LastSeen      = reader.IsDBNull(7)
                            ? "Never"
                            : reader.GetDateTime(7)
                                    .ToString("dd MMM yyyy HH:mm")
                    });
                }

                return new CustomerListResponse
                {
                    Success   = true,
                    Customers = customers,
                    Total     = customers.Count,
                    Active    = customers.Count(c => c.IsActive),
                    Inactive  = customers.Count(c => !c.IsActive),
                    Message   = $"{customers.Count} customer(s) found."
                };
            }
            catch (Exception ex)
            {
                return new CustomerListResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        // ── CREATE CUSTOMER ──────────────────────────────────────────
        public async Task<CustomerActionResponse> CreateCustomerAsync(
            CreateCustomerRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.FullName) ||
                    string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return new CustomerActionResponse
                    {
                        Success = false,
                        Message = "All fields are required."
                    };
                }

                if (request.Password.Length < 6)
                {
                    return new CustomerActionResponse
                    {
                        Success = false,
                        Message = "Password must be at least 6 characters."
                    };
                }

                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check username uniqueness
                string checkQuery = @"
                    SELECT COUNT(*) FROM Users
                    WHERE Username = @Username";

                using var checkCmd =
                    new SqlCommand(checkQuery, connection);
                checkCmd.Parameters.AddWithValue(
                    "@Username", request.Username);

                int exists = (int)(await checkCmd.ExecuteScalarAsync())!;
                if (exists > 0)
                {
                    return new CustomerActionResponse
                    {
                        Success = false,
                        Message =
                            $"Username '{request.Username}' already exists."
                    };
                }

                string hash = BCrypt.Net.BCrypt
                    .HashPassword(request.Password);

                string insertQuery = @"
                    INSERT INTO Users
                        (FullName, Username, PasswordHash, Role, IsActive)
                    VALUES
                        (@FullName, @Username, @Hash, 'Customer', 1)";

                using var insertCmd =
                    new SqlCommand(insertQuery, connection);
                insertCmd.Parameters.AddWithValue(
                    "@FullName", request.FullName);
                insertCmd.Parameters.AddWithValue(
                    "@Username", request.Username);
                insertCmd.Parameters.AddWithValue("@Hash", hash);

                await insertCmd.ExecuteNonQueryAsync();

                return new CustomerActionResponse
                {
                    Success = true,
                    Message =
                        $"Customer '{request.FullName}' created successfully."
                };
            }
            catch (Exception ex)
            {
                return new CustomerActionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        // ── UPDATE CUSTOMER ──────────────────────────────────────────
        public async Task<CustomerActionResponse> UpdateCustomerAsync(
            UpdateCustomerRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.FullName) ||
                    string.IsNullOrWhiteSpace(request.Username))
                {
                    return new CustomerActionResponse
                    {
                        Success = false,
                        Message = "Full name and username are required."
                    };
                }

                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check username uniqueness (exclude current user)
                string checkQuery = @"
                    SELECT COUNT(*) FROM Users
                    WHERE Username = @Username
                    AND   UserId  != @UserId";

                using var checkCmd =
                    new SqlCommand(checkQuery, connection);
                checkCmd.Parameters.AddWithValue(
                    "@Username", request.Username);
                checkCmd.Parameters.AddWithValue(
                    "@UserId", request.UserId);

                int exists = (int)(await checkCmd.ExecuteScalarAsync())!;
                if (exists > 0)
                {
                    return new CustomerActionResponse
                    {
                        Success = false,
                        Message =
                            $"Username '{request.Username}' is taken."
                    };
                }

                // Build update query
                string updateQuery;
                SqlCommand updateCmd;

                if (!string.IsNullOrWhiteSpace(request.Password))
                {
                    if (request.Password.Length < 6)
                    {
                        return new CustomerActionResponse
                        {
                            Success = false,
                            Message =
                                "Password must be at least 6 characters."
                        };
                    }

                    string hash = BCrypt.Net.BCrypt
                        .HashPassword(request.Password);

                    updateQuery = @"
                        UPDATE Users
                        SET FullName     = @FullName,
                            Username     = @Username,
                            PasswordHash = @Hash
                        WHERE UserId = @UserId";

                    updateCmd = new SqlCommand(updateQuery, connection);
                    updateCmd.Parameters.AddWithValue("@Hash", hash);
                }
                else
                {
                    updateQuery = @"
                        UPDATE Users
                        SET FullName = @FullName,
                            Username = @Username
                        WHERE UserId = @UserId";

                    updateCmd = new SqlCommand(updateQuery, connection);
                }

                updateCmd.Parameters.AddWithValue(
                    "@FullName", request.FullName);
                updateCmd.Parameters.AddWithValue(
                    "@Username", request.Username);
                updateCmd.Parameters.AddWithValue(
                    "@UserId", request.UserId);

                await updateCmd.ExecuteNonQueryAsync();
                updateCmd.Dispose();

                return new CustomerActionResponse
                {
                    Success = true,
                    Message = "Customer updated successfully."
                };
            }
            catch (Exception ex)
            {
                return new CustomerActionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        // ── TOGGLE ACTIVE STATUS ─────────────────────────────────────
        public async Task<CustomerActionResponse> ToggleStatusAsync(
            int userId)
        {
            try
            {
                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Get current status
                string selectQuery = @"
                    SELECT IsActive, FullName FROM Users
                    WHERE UserId = @UserId";

                using var selectCmd =
                    new SqlCommand(selectQuery, connection);
                selectCmd.Parameters.AddWithValue("@UserId", userId);

                bool   currentStatus;
                string fullName;

                using var reader = await selectCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new CustomerActionResponse
                    {
                        Success = false,
                        Message = "Customer not found."
                    };
                }

                currentStatus = reader.GetBoolean(0);
                fullName      = reader.GetString(1);
                reader.Close();

                bool newStatus = !currentStatus;

                string updateQuery = @"
                    UPDATE Users SET IsActive = @IsActive
                    WHERE UserId = @UserId";

                using var updateCmd =
                    new SqlCommand(updateQuery, connection);
                updateCmd.Parameters.AddWithValue("@IsActive", newStatus);
                updateCmd.Parameters.AddWithValue("@UserId",   userId);

                await updateCmd.ExecuteNonQueryAsync();

                string action = newStatus ? "activated" : "deactivated";

                return new CustomerActionResponse
                {
                    Success = true,
                    Message = $"'{fullName}' has been {action}."
                };
            }
            catch (Exception ex)
            {
                return new CustomerActionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        // ── DELETE CUSTOMER ──────────────────────────────────────────
        public async Task<CustomerActionResponse> DeleteCustomerAsync(
            int userId)
        {
            try
            {
                using var connection =
                    new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Check for active sessions
                string checkSession = @"
                    SELECT COUNT(*) FROM Sessions
                    WHERE UserId = @UserId AND Status = 'Active'";

                using var checkCmd =
                    new SqlCommand(checkSession, connection);
                checkCmd.Parameters.AddWithValue("@UserId", userId);

                int activeSessions =
                    (int)(await checkCmd.ExecuteScalarAsync())!;

                if (activeSessions > 0)
                {
                    return new CustomerActionResponse
                    {
                        Success = false,
                        Message =
                            "Cannot delete customer with an active session. " +
                            "Terminate the session first."
                    };
                }

                // Delete in order: Alerts, Logs, Billing, Sessions, User
                string[] deleteQueries =
                {
                    "DELETE FROM Alerts  WHERE UserId = @UserId",
                    "DELETE FROM Logs    WHERE UserId = @UserId",
                    "DELETE FROM Billing WHERE UserId = @UserId",
                    "DELETE FROM Sessions WHERE UserId = @UserId",
                    "DELETE FROM Users    WHERE UserId = @UserId " +
                    "AND Role = 'Customer'"
                };

                foreach (string q in deleteQueries)
                {
                    using var cmd = new SqlCommand(q, connection);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    await cmd.ExecuteNonQueryAsync();
                }

                return new CustomerActionResponse
                {
                    Success = true,
                    Message = "Customer deleted successfully."
                };
            }
            catch (Exception ex)
            {
                return new CustomerActionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }
    }
}
