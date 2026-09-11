using System;
using System.Collections.Generic;

namespace SessionManagement.Shared.DTOs
{
    public class SecurityRuleDto
    {
        public int RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
        public string Scope { get; set; } = "AllPCs"; // SamePC, SameUser, SameSession, SameIP, SelectedPCs, AllPCs
        public string? TargetScopeValue { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDraft { get; set; } = false;
        public bool RequiresApproval { get; set; } = false;
        public int CooldownMinutes { get; set; } = 5;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "System";

        public List<RuleConditionDto> Conditions { get; set; } = new();
        public List<RuleActionDto> Actions { get; set; } = new();
    }

    public class RuleConditionDto
    {
        public int ConditionId { get; set; }
        public int RuleId { get; set; }
        public string MetricType { get; set; } = "FailedLogin";
        public string Operator { get; set; } = "CountInWindow"; // Equals, NotEquals, GreaterThan, GreaterThanOrEqual, LessThan, CountInWindow, Exists
        public string ThresholdValue { get; set; } = "5";
        public int WindowMinutes { get; set; } = 10;
    }

    public class RuleActionDto
    {
        public int ActionId { get; set; }
        public int RuleId { get; set; }
        public string ActionType { get; set; } = "CreateAlert"; // CreateAlert, NotifyAdmin, RequestAiAnalysis, BlockNewSession, MarkClientUnderReview, LockClient, PauseSession, TerminateSession, DisconnectNetwork, RestartClient, ShutdownClient
        public bool RequiresApproval { get; set; } = false;
    }
}
