namespace SessionManagement.Shared.DTOs
{
    public class SecurityAlert
    {
        public int    AlertId     { get; set; }
        public int?   UserId      { get; set; }
        public string Username    { get; set; } = string.Empty;
        public string AlertType   { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity    { get; set; } = string.Empty;
        public bool   IsRead      { get; set; }
        public string CreatedAt   { get; set; } = string.Empty;

        // Display helpers
        public string SeverityIcon => Severity switch
        {
            "High"   => "🔴",
            "Medium" => "🟡",
            "Low"    => "🟢",
            _        => "⚪"
        };

        public string TypeIcon => AlertType switch
        {
            "FailedLogin"        => "🔐",
            "DuplicateSession"   => "🔄",
            "RateLimit"          => "⚡",
            "SessionTerminated"  => "🛑",
            "SessionStart"       => "▶️",
            "SessionEnd"         => "⏹️",
            _                    => "📋"
        };

        public string ReadIcon => IsRead ? "✅" : "🔵";

        public string DisplayTitle =>
            $"{SeverityIcon}  [{Severity}]  {AlertType}";

        // AI Analysis Properties
        public int?   AIThreatScore       { get; set; }
        public string? AIClassification   { get; set; }
        public string? AIRecommendedAction{ get; set; }
        public string? AIExplanation      { get; set; }

        public string AIThreatBadge => AIThreatScore.HasValue
            ? (AIThreatScore >= 75 ? "🔴 HIGH THREAT" : AIThreatScore >= 40 ? "🟡 MODERATE THREAT" : "🟢 LOW THREAT")
            : "⚪ UNANALYZED";
    }
}
