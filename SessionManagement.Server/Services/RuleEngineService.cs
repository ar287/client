using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SessionManagement.Server.Hubs;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class RuleEngineService
    {
        private readonly string _connectionString;
        private readonly ILogger<RuleEngineService> _logger;
        private readonly IHubContext<AlertHub> _alertHub;

        public RuleEngineService(
            string connectionString,
            ILogger<RuleEngineService> logger,
            IHubContext<AlertHub> alertHub)
        {
            _connectionString = connectionString;
            _logger = logger;
            _alertHub = alertHub;
            EnsureDefaultRulesSeeded();
        }

        private void EnsureDefaultRulesSeeded()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                string checkSql = "SELECT COUNT(*) FROM sys.tables WHERE name = 'SecurityRules'";
                using var checkCmd = new SqlCommand(checkSql, conn);
                int count = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0);
                if (count == 0) return;

                string countRulesSql = "SELECT COUNT(*) FROM SecurityRules";
                using var countRulesCmd = new SqlCommand(countRulesSql, conn);
                int existingRules = Convert.ToInt32(countRulesCmd.ExecuteScalar() ?? 0);
                if (existingRules > 0) return;

                // Seed Default Rule 1: Failed Logins
                string seedSql = @"
                INSERT INTO SecurityRules (RuleName, Description, Severity, Scope, IsActive, CooldownMinutes)
                VALUES ('Multiple Failed Logins', 'Triggers when 5 failed login attempts occur within 10 minutes.', 'Medium', 'SamePC', 1, 5);
                DECLARE @R1 INT = SCOPE_IDENTITY();
                INSERT INTO RuleConditions (RuleId, MetricType, Operator, ThresholdValue, WindowMinutes)
                VALUES (@R1, 'FailedLogin', 'CountInWindow', '5', 10);
                INSERT INTO RuleActions (RuleId, ActionType, RequiresApproval)
                VALUES (@R1, 'CreateAlert', 0), (@R1, 'RequestAiAnalysis', 0);

                -- Seed Default Rule 2: Security Violations
                INSERT INTO SecurityRules (RuleName, Description, Severity, Scope, IsActive, CooldownMinutes)
                VALUES ('Security Violation Detected', 'Triggers on unauthorized process or lock bypass attempt.', 'High', 'SamePC', 1, 5);
                DECLARE @R2 INT = SCOPE_IDENTITY();
                INSERT INTO RuleConditions (RuleId, MetricType, Operator, ThresholdValue, WindowMinutes)
                VALUES (@R2, 'SecurityAlert', 'CountInWindow', '1', 10);
                INSERT INTO RuleActions (RuleId, ActionType, RequiresApproval)
                VALUES (@R2, 'CreateAlert', 0), (@R2, 'RequestAiAnalysis', 0), (@R2, 'MarkClientUnderReview', 0);";

                using var seedCmd = new SqlCommand(seedSql, conn);
                seedCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Default security rules seed check skipped or failed.");
            }
        }

        public async Task<List<SecurityRuleDto>> GetRulesAsync()
        {
            var rules = new List<SecurityRuleDto>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sql = "SELECT RuleId, RuleName, Description, Severity, Scope, TargetScopeValue, IsActive, IsDraft, RequiresApproval, CooldownMinutes, CreatedAtUtc, CreatedBy FROM SecurityRules ORDER BY RuleId ASC";
                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    rules.Add(new SecurityRuleDto
                    {
                        RuleId = reader.GetInt32(0),
                        RuleName = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Severity = reader.GetString(3),
                        Scope = reader.GetString(4),
                        TargetScopeValue = reader.IsDBNull(5) ? null : reader.GetString(5),
                        IsActive = reader.GetBoolean(6),
                        IsDraft = reader.GetBoolean(7),
                        RequiresApproval = reader.GetBoolean(8),
                        CooldownMinutes = reader.GetInt32(9),
                        CreatedAtUtc = reader.GetDateTime(10),
                        CreatedBy = reader.GetString(11)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve security rules.");
            }

            return rules;
        }

        public async Task<RiskAssessmentDto> EvaluateClientRiskAsync(string clientId)
        {
            var assessment = new RiskAssessmentDto
            {
                ClientId = clientId,
                EvaluatedBy = "RuleEngine",
                ConfidenceScore = 1.0
            };

            int score = 0;
            var reasons = new List<string>();
            var evidenceIds = new List<long>();

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Fetch recent events for this client in the last 15 minutes
                string sqlEvents = @"
                SELECT EventId, EventType, EventName, Severity, TimestampUtc, Message
                FROM ActivityEvents
                WHERE ClientId = @ClientId AND TimestampUtc >= DATEADD(MINUTE, -15, GETUTCDATE())
                ORDER BY TimestampUtc DESC";

                int failedLogins = 0;
                int securityAlerts = 0;
                int terminations = 0;

                using (var cmd = new SqlCommand(sqlEvents, conn))
                {
                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        long eid = reader.GetInt64(0);
                        string etype = reader.GetString(1);

                        evidenceIds.Add(eid);
                        if (etype.Equals("FailedLogin", StringComparison.OrdinalIgnoreCase)) failedLogins++;
                        if (etype.Equals("SecurityAlert", StringComparison.OrdinalIgnoreCase)) securityAlerts++;
                        if (etype.Equals("SessionTerminated", StringComparison.OrdinalIgnoreCase)) terminations++;
                    }
                }

                // Deterministic Scoring Engine Rules:
                if (failedLogins >= 5)
                {
                    score += 35;
                    reasons.Add($"High frequency of failed logins ({failedLogins} attempts in last 15 min).");
                }
                else if (failedLogins >= 2)
                {
                    score += 15;
                    reasons.Add($"Multiple failed logins detected ({failedLogins} attempts).");
                }

                if (securityAlerts >= 1)
                {
                    score += 40 * securityAlerts;
                    reasons.Add($"Active security alert detected ({securityAlerts} alerts).");
                }

                if (terminations >= 2)
                {
                    score += 20;
                    reasons.Add($"Repeated abrupt session terminations ({terminations} terminations).");
                }

                // Cap score between 0 and 100
                score = Math.Min(100, Math.Max(0, score));
                assessment.RiskScore = score;
                assessment.Reasons = reasons;
                assessment.EvidenceEventIds = evidenceIds;

                // Determine RiskLevel
                assessment.RiskLevel = score switch
                {
                    >= 80 => "Critical",
                    >= 60 => "High",
                    >= 30 => "Medium",
                    _ => "Low"
                };

                assessment.RecommendedAction = assessment.RiskLevel switch
                {
                    "Critical" => "Lock PC immediately & audit IP access.",
                    "High" => "Inspect client process logs & request admin review.",
                    "Medium" => "Monitor user session activity closely.",
                    _ => "Standard operational session."
                };

                // Persist assessment to DB if table exists
                await SaveRiskAssessmentAsync(conn, assessment);

                // Push real-time alert to Admins if risk is Medium or above
                if (score >= 30)
                {
                    await PushRuleAlertAsync(assessment);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate client risk for {ClientId}", clientId);
            }

            return assessment;
        }

        private async Task SaveRiskAssessmentAsync(SqlConnection conn, RiskAssessmentDto dto)
        {
            try
            {
                string sql = @"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'RiskAssessments')
                BEGIN
                    INSERT INTO RiskAssessments 
                    (ClientId, UserId, RiskScore, RiskLevel, ReasonsJson, EvidenceEvents, RecommendedAction, EvaluatedBy, ConfidenceScore, CreatedAtUtc)
                    VALUES 
                    (@ClientId, @UserId, @RiskScore, @RiskLevel, @ReasonsJson, @EvidenceEvents, @RecommendedAction, @EvaluatedBy, @ConfidenceScore, GETUTCDATE());
                END";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ClientId", dto.ClientId);
                cmd.Parameters.AddWithValue("@UserId", (object?)dto.UserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RiskScore", dto.RiskScore);
                cmd.Parameters.AddWithValue("@RiskLevel", dto.RiskLevel);
                cmd.Parameters.AddWithValue("@ReasonsJson", JsonSerializer.Serialize(dto.Reasons));
                cmd.Parameters.AddWithValue("@EvidenceEvents", JsonSerializer.Serialize(dto.EvidenceEventIds));
                cmd.Parameters.AddWithValue("@RecommendedAction", dto.RecommendedAction);
                cmd.Parameters.AddWithValue("@EvaluatedBy", dto.EvaluatedBy);
                cmd.Parameters.AddWithValue("@ConfidenceScore", dto.ConfidenceScore);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RiskAssessment persistence failed.");
            }
        }

        public async Task<bool> CreateRuleAsync(SecurityRuleDto dto)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                string sqlRule = @"
                INSERT INTO SecurityRules (RuleName, Description, Severity, Scope, TargetScopeValue, IsActive, IsDraft, RequiresApproval, CooldownMinutes, CreatedAtUtc, CreatedBy)
                VALUES (@RuleName, @Description, @Severity, @Scope, @TargetScopeValue, @IsActive, @IsDraft, @RequiresApproval, @CooldownMinutes, GETUTCDATE(), @CreatedBy);
                SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(sqlRule, conn);
                cmd.Parameters.AddWithValue("@RuleName", dto.RuleName);
                cmd.Parameters.AddWithValue("@Description", dto.Description ?? "");
                cmd.Parameters.AddWithValue("@Severity", dto.Severity ?? "Medium");
                cmd.Parameters.AddWithValue("@Scope", dto.Scope ?? "AllPCs");
                cmd.Parameters.AddWithValue("@TargetScopeValue", (object?)dto.TargetScopeValue ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IsActive", dto.IsActive);
                cmd.Parameters.AddWithValue("@IsDraft", dto.IsDraft);
                cmd.Parameters.AddWithValue("@RequiresApproval", dto.RequiresApproval);
                cmd.Parameters.AddWithValue("@CooldownMinutes", dto.CooldownMinutes > 0 ? dto.CooldownMinutes : 5);
                cmd.Parameters.AddWithValue("@CreatedBy", string.IsNullOrWhiteSpace(dto.CreatedBy) ? "Admin" : dto.CreatedBy);

                object? newIdObj = await cmd.ExecuteScalarAsync();
                int ruleId = Convert.ToInt32(newIdObj ?? 0);

                if (ruleId > 0 && dto.Conditions != null)
                {
                    foreach (var c in dto.Conditions)
                    {
                        string sqlCond = @"
                        INSERT INTO RuleConditions (RuleId, MetricType, Operator, ThresholdValue, WindowMinutes)
                        VALUES (@RuleId, @MetricType, @Operator, @ThresholdValue, @WindowMinutes);";

                        using var cmdCond = new SqlCommand(sqlCond, conn);
                        cmdCond.Parameters.AddWithValue("@RuleId", ruleId);
                        cmdCond.Parameters.AddWithValue("@MetricType", c.MetricType);
                        cmdCond.Parameters.AddWithValue("@Operator", c.Operator ?? "CountInWindow");
                        cmdCond.Parameters.AddWithValue("@ThresholdValue", c.ThresholdValue ?? "5");
                        cmdCond.Parameters.AddWithValue("@WindowMinutes", c.WindowMinutes > 0 ? c.WindowMinutes : 10);
                        await cmdCond.ExecuteNonQueryAsync();
                    }
                }

                if (ruleId > 0 && dto.Actions != null)
                {
                    foreach (var a in dto.Actions)
                    {
                        string sqlAct = @"
                        INSERT INTO RuleActions (RuleId, ActionType, RequiresApproval)
                        VALUES (@RuleId, @ActionType, @RequiresApproval);";

                        using var cmdAct = new SqlCommand(sqlAct, conn);
                        cmdAct.Parameters.AddWithValue("@RuleId", ruleId);
                        cmdAct.Parameters.AddWithValue("@ActionType", a.ActionType);
                        cmdAct.Parameters.AddWithValue("@RequiresApproval", a.RequiresApproval);
                        await cmdAct.ExecuteNonQueryAsync();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create security rule: {RuleName}", dto.RuleName);
                return false;
            }
        }

        /// <summary>
        /// Broadcasts a real-time SecurityAlertNotification to all Admin clients
        /// connected to AlertHub when a risk assessment exceeds the threshold.
        /// </summary>
        private async Task PushRuleAlertAsync(RiskAssessmentDto assessment)
        {
            try
            {
                var notification = new SecurityAlertNotification
                {
                    AlertId         = Guid.NewGuid().ToString(),
                    ClientId        = assessment.ClientId,
                    ClientName      = assessment.ClientId, // enriched by client if available
                    RuleName        = "Rule Engine Evaluation",
                    AlertType       = "RuleFired",
                    Severity        = assessment.RiskLevel,
                    Message         = assessment.Reasons.Count > 0
                                        ? string.Join(" | ", assessment.Reasons)
                                        : $"Risk score {assessment.RiskScore} detected on {assessment.ClientId}",
                    ActionTaken     = assessment.RecommendedAction,
                    RequiresApproval = false,
                    RiskScore       = assessment.RiskScore,
                    TimestampUtc    = DateTime.UtcNow
                };

                await _alertHub.Clients.Group("AlertAdmins")
                    .SendAsync("RuleAlert", notification);

                _logger.LogInformation("[AlertHub] Pushed live alert for {ClientId} — Score: {Score} ({Level})",
                    assessment.ClientId, assessment.RiskScore, assessment.RiskLevel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AlertHub] Failed to push live alert for {ClientId}", assessment.ClientId);
            }
        }
    }
}
