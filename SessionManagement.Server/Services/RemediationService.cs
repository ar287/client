using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SessionManagement.Server.Hubs;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class RemediationService
    {
        private readonly string _connectionString;
        private readonly ILogger<RemediationService> _logger;
        private readonly IHubContext<SessionHub> _sessionHub;

        public RemediationService(
            string connectionString,
            ILogger<RemediationService> logger,
            IHubContext<SessionHub> sessionHub)
        {
            _connectionString = connectionString;
            _logger = logger;
            _sessionHub = sessionHub;
            EnsureTableCreated();
        }

        private void EnsureTableCreated()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RemediationLogs')
                BEGIN
                    CREATE TABLE RemediationLogs (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        ClientId NVARCHAR(100) NOT NULL,
                        ActionType NVARCHAR(50) NOT NULL,
                        TargetProcessName NVARCHAR(150) NULL,
                        Reason NVARCHAR(MAX) NULL,
                        InitiatedBy NVARCHAR(100) NOT NULL,
                        RiskScore INT NOT NULL DEFAULT 0,
                        IsSuccess BIT NOT NULL DEFAULT 1,
                        Message NVARCHAR(MAX) NULL,
                        ExecutedAtUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END";

                using var cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RemediationLogs table creation check failed or skipped.");
            }
        }

        public async Task<RemediationLogDto> ExecuteRemediationAsync(RemediationRequestDto request)
        {
            var resultLog = new RemediationLogDto
            {
                ClientId = request.ClientId,
                ActionType = request.ActionType,
                TargetProcessName = request.TargetProcessName,
                Reason = request.Reason,
                InitiatedBy = request.InitiatedBy,
                RiskScore = request.RiskScore,
                IsSuccess = true,
                ExecutedAtUtc = DateTime.UtcNow
            };

            try
            {
                // Send SignalR real-time command to Client PCs
                await _sessionHub.Clients.All.SendAsync(
                    "RemediationCommand",
                    request.ActionType,
                    request.TargetProcessName ?? "",
                    request.Reason ?? "Remediation policy triggered."
                );

                resultLog.Message = $"Remediation signal [{request.ActionType}] dispatched successfully to {request.ClientId}.";
                _logger.LogInformation("[RemediationService] Dispatched {Action} for {ClientId}", request.ActionType, request.ClientId);
            }
            catch (Exception ex)
            {
                resultLog.IsSuccess = false;
                resultLog.Message = $"Failed to dispatch signal: {ex.Message}";
                _logger.LogError(ex, "Failed to send remediation command for {ClientId}", request.ClientId);
            }

            // Save execution result to DB
            await SaveRemediationLogAsync(resultLog);

            return resultLog;
        }

        private async Task SaveRemediationLogAsync(RemediationLogDto dto)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                INSERT INTO RemediationLogs (ClientId, ActionType, TargetProcessName, Reason, InitiatedBy, RiskScore, IsSuccess, Message, ExecutedAtUtc)
                VALUES (@ClientId, @ActionType, @TargetProcessName, @Reason, @InitiatedBy, @RiskScore, @IsSuccess, @Message, GETUTCDATE());
                SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ClientId", dto.ClientId);
                cmd.Parameters.AddWithValue("@ActionType", dto.ActionType);
                cmd.Parameters.AddWithValue("@TargetProcessName", (object?)dto.TargetProcessName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Reason", (object?)dto.Reason ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@InitiatedBy", dto.InitiatedBy);
                cmd.Parameters.AddWithValue("@RiskScore", dto.RiskScore);
                cmd.Parameters.AddWithValue("@IsSuccess", dto.IsSuccess);
                cmd.Parameters.AddWithValue("@Message", (object?)dto.Message ?? DBNull.Value);

                object? newId = await cmd.ExecuteScalarAsync();
                dto.Id = Convert.ToInt32(newId ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist RemediationLog.");
            }
        }

        public async Task<List<RemediationLogDto>> GetRemediationLogsAsync(string? clientId = null)
        {
            var list = new List<RemediationLogDto>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                SELECT Id, ClientId, ActionType, TargetProcessName, Reason, InitiatedBy, RiskScore, IsSuccess, Message, ExecutedAtUtc
                FROM RemediationLogs";

                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    sql += " WHERE ClientId = @ClientId";
                }

                sql += " ORDER BY ExecutedAtUtc DESC";

                using var cmd = new SqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new RemediationLogDto
                    {
                        Id = reader.GetInt32(0),
                        ClientId = reader.GetString(1),
                        ActionType = reader.GetString(2),
                        TargetProcessName = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Reason = reader.IsDBNull(4) ? null : reader.GetString(4),
                        InitiatedBy = reader.GetString(5),
                        RiskScore = reader.GetInt32(6),
                        IsSuccess = reader.GetBoolean(7),
                        Message = reader.IsDBNull(8) ? null : reader.GetString(8),
                        ExecutedAtUtc = reader.GetDateTime(9)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch remediation logs.");
            }

            return list;
        }
    }
}
