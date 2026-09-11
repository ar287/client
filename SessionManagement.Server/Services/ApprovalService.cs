using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class ApprovalService
    {
        private readonly string _connectionString;
        private readonly ILogger<ApprovalService> _logger;

        public ApprovalService(string connectionString, ILogger<ApprovalService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
            EnsureTableCreated();
        }

        private void EnsureTableCreated()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PendingApprovals')
                BEGIN
                    CREATE TABLE PendingApprovals (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        RuleId INT NOT NULL,
                        RuleName NVARCHAR(150) NOT NULL,
                        ClientId NVARCHAR(100) NOT NULL,
                        ActionType NVARCHAR(50) NOT NULL,
                        ActionTarget NVARCHAR(200) NULL,
                        RiskScore INT NOT NULL DEFAULT 0,
                        Reason NVARCHAR(MAX) NULL,
                        Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
                        RequestedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        ReviewedAtUtc DATETIME2 NULL,
                        ReviewedBy NVARCHAR(100) NULL,
                        ReviewNotes NVARCHAR(MAX) NULL
                    );
                END";

                using var cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PendingApprovals table creation check failed or skipped.");
            }
        }

        public async Task<List<PendingApprovalDto>> GetPendingApprovalsAsync()
        {
            var list = new List<PendingApprovalDto>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                SELECT Id, RuleId, RuleName, ClientId, ActionType, ActionTarget, RiskScore, Reason, Status, RequestedAtUtc, ReviewedAtUtc, ReviewedBy, ReviewNotes
                FROM PendingApprovals
                ORDER BY Status ASC, RequestedAtUtc DESC";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(new PendingApprovalDto
                    {
                        Id = reader.GetInt32(0),
                        RuleId = reader.GetInt32(1),
                        RuleName = reader.GetString(2),
                        ClientId = reader.GetString(3),
                        ActionType = reader.GetString(4),
                        ActionTarget = reader.IsDBNull(5) ? null : reader.GetString(5),
                        RiskScore = reader.GetInt32(6),
                        Reason = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Status = reader.GetString(8),
                        RequestedAtUtc = reader.GetDateTime(9),
                        ReviewedAtUtc = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                        ReviewedBy = reader.IsDBNull(11) ? null : reader.GetString(11),
                        ReviewNotes = reader.IsDBNull(12) ? null : reader.GetString(12)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve pending approvals.");
            }

            return list;
        }

        public async Task<int> QueueApprovalAsync(PendingApprovalDto dto)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                INSERT INTO PendingApprovals (RuleId, RuleName, ClientId, ActionType, ActionTarget, RiskScore, Reason, Status, RequestedAtUtc)
                VALUES (@RuleId, @RuleName, @ClientId, @ActionType, @ActionTarget, @RiskScore, @Reason, 'Pending', GETUTCDATE());
                SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@RuleId", dto.RuleId);
                cmd.Parameters.AddWithValue("@RuleName", dto.RuleName ?? "Security Rule");
                cmd.Parameters.AddWithValue("@ClientId", dto.ClientId);
                cmd.Parameters.AddWithValue("@ActionType", dto.ActionType);
                cmd.Parameters.AddWithValue("@ActionTarget", (object?)dto.ActionTarget ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RiskScore", dto.RiskScore);
                cmd.Parameters.AddWithValue("@Reason", (object?)dto.Reason ?? DBNull.Value);

                object? newId = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(newId ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue approval for rule {RuleId} client {ClientId}", dto.RuleId, dto.ClientId);
                return 0;
            }
        }

        public async Task<bool> ProcessApprovalAsync(int id, string newStatus, string reviewedBy, string reviewNotes)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                UPDATE PendingApprovals
                SET Status = @Status, ReviewedAtUtc = GETUTCDATE(), ReviewedBy = @ReviewedBy, ReviewNotes = @ReviewNotes
                WHERE Id = @Id AND Status = 'Pending'";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Status", newStatus);
                cmd.Parameters.AddWithValue("@ReviewedBy", string.IsNullOrWhiteSpace(reviewedBy) ? "Admin" : reviewedBy);
                cmd.Parameters.AddWithValue("@ReviewNotes", (object?)reviewNotes ?? DBNull.Value);

                int rows = await cmd.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process approval ID {Id} to {Status}", id, newStatus);
                return false;
            }
        }
    }
}
