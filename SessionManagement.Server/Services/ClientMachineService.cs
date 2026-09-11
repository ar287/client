using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class ClientMachineService
    {
        private readonly string _connectionString;
        private readonly ILogger<ClientMachineService> _logger;

        public ClientMachineService(string connectionString, ILogger<ClientMachineService> logger)
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
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ClientMachines')
                BEGIN
                    CREATE TABLE ClientMachines (
                        MachineId        INT IDENTITY(1,1) PRIMARY KEY,
                        ClientId         NVARCHAR(100)  NOT NULL UNIQUE,
                        MachineName      NVARCHAR(100)  NOT NULL,
                        IPAddress        NVARCHAR(50)   NULL,
                        MacAddress       NVARCHAR(50)   NULL,
                        OSVersion        NVARCHAR(100)  NULL,
                        Status           NVARCHAR(20)   NOT NULL DEFAULT 'Offline'
                                         CHECK (Status IN ('Online', 'Offline', 'InUse', 'Locked', 'UnderReview')),
                        LastHeartbeatUtc DATETIME       NULL,
                        CurrentUserId    INT            NULL,
                        CurrentSessionId INT            NULL,
                        RegisteredAtUtc  DATETIME       NOT NULL DEFAULT GETUTCDATE()
                    );

                    CREATE INDEX IX_ClientMachines_ClientId ON ClientMachines(ClientId);
                    CREATE INDEX IX_ClientMachines_Status ON ClientMachines(Status);
                END";

                using var cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify/create ClientMachines table automatically.");
            }
        }

        public async Task<ClientMachineDto> RegisterOrUpdateMachineAsync(HeartbeatRequest request)
        {
            var machine = new ClientMachineDto
            {
                ClientId = request.ClientId,
                MachineName = string.IsNullOrWhiteSpace(request.MachineName) ? request.ClientId : request.MachineName,
                IPAddress = request.IPAddress,
                MacAddress = request.MacAddress,
                OSVersion = request.OSVersion,
                Status = string.IsNullOrWhiteSpace(request.Status) ? "Online" : request.Status,
                LastHeartbeatUtc = DateTime.UtcNow,
                CurrentUserId = request.CurrentUserId,
                CurrentSessionId = request.CurrentSessionId
            };

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                IF EXISTS (SELECT 1 FROM ClientMachines WHERE ClientId = @ClientId)
                BEGIN
                    UPDATE ClientMachines 
                    SET MachineName = @MachineName,
                        IPAddress = ISNULL(@IPAddress, IPAddress),
                        MacAddress = ISNULL(@MacAddress, MacAddress),
                        OSVersion = ISNULL(@OSVersion, OSVersion),
                        Status = @Status,
                        LastHeartbeatUtc = @LastHeartbeatUtc,
                        CurrentUserId = @CurrentUserId,
                        CurrentSessionId = @CurrentSessionId
                    WHERE ClientId = @ClientId;
                END
                ELSE
                BEGIN
                    INSERT INTO ClientMachines 
                    (ClientId, MachineName, IPAddress, MacAddress, OSVersion, Status, LastHeartbeatUtc, CurrentUserId, CurrentSessionId, RegisteredAtUtc)
                    VALUES 
                    (@ClientId, @MachineName, @IPAddress, @MacAddress, @OSVersion, @Status, @LastHeartbeatUtc, @CurrentUserId, @CurrentSessionId, GETUTCDATE());
                END";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ClientId", machine.ClientId);
                cmd.Parameters.AddWithValue("@MachineName", machine.MachineName);
                cmd.Parameters.AddWithValue("@IPAddress", (object?)machine.IPAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MacAddress", (object?)machine.MacAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OSVersion", (object?)machine.OSVersion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", machine.Status);
                cmd.Parameters.AddWithValue("@LastHeartbeatUtc", machine.LastHeartbeatUtc.Value);
                cmd.Parameters.AddWithValue("@CurrentUserId", (object?)machine.CurrentUserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CurrentSessionId", (object?)machine.CurrentSessionId ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register or update machine: {ClientId}", request.ClientId);
            }

            return machine;
        }

        public async Task<List<ClientMachineDto>> GetAllMachinesAsync()
        {
            var list = new List<ClientMachineDto>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                SELECT cm.MachineId, cm.ClientId, cm.MachineName, cm.IPAddress, cm.MacAddress, cm.OSVersion, 
                       cm.Status, cm.LastHeartbeatUtc, cm.CurrentUserId, cm.CurrentSessionId, cm.RegisteredAtUtc,
                       u.Username
                FROM ClientMachines cm
                LEFT JOIN Users u ON cm.CurrentUserId = u.UserId
                ORDER BY cm.ClientId ASC";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var dto = new ClientMachineDto
                    {
                        MachineId = reader.GetInt32(0),
                        ClientId = reader.GetString(1),
                        MachineName = reader.GetString(2),
                        IPAddress = reader.IsDBNull(3) ? null : reader.GetString(3),
                        MacAddress = reader.IsDBNull(4) ? null : reader.GetString(4),
                        OSVersion = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Status = reader.GetString(6),
                        LastHeartbeatUtc = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                        CurrentUserId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                        CurrentSessionId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                        RegisteredAtUtc = reader.GetDateTime(10),
                        CurrentUsername = reader.IsDBNull(11) ? null : reader.GetString(11)
                    };

                    // Auto mark offline if heartbeat older than 60s
                    if (dto.Status != "Offline" && (!dto.LastHeartbeatUtc.HasValue || (DateTime.UtcNow - dto.LastHeartbeatUtc.Value).TotalSeconds > 60))
                    {
                        dto.Status = "Offline";
                    }

                    list.Add(dto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve client machines list.");
            }

            return list;
        }

        public async Task<ClientMachineDto?> GetMachineByClientIdAsync(string clientId)
        {
            var list = await GetAllMachinesAsync();
            return list.Find(m => m.ClientId.Equals(clientId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task MarkStaleMachinesOfflineAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = @"
                UPDATE ClientMachines
                SET Status = 'Offline'
                WHERE Status != 'Offline'
                AND (LastHeartbeatUtc IS NULL OR DATEDIFF(SECOND, LastHeartbeatUtc, GETUTCDATE()) > 60)";

                using var cmd = new SqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark stale client machines offline.");
            }
        }
    }
}
