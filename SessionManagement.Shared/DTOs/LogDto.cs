namespace SessionManagement.Shared.DTOs
{
    public class LogDto
    {
        public int    LogId       { get; set; }
        public int?   UserId      { get; set; }
        public int?   SessionId   { get; set; }
        public string Username    { get; set; } = string.Empty;
        public string FullName    { get; set; } = string.Empty;
        public string EventType   { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IPAddress   { get; set; } = string.Empty;
        public string CreatedAt   { get; set; } = string.Empty;

        // Display helpers
        public string EventIcon => EventType switch
        {
            "Login"             => "🔓",
            "Logout"            => "🔒",
            "LoginFailed"       => "⛔",
            "SessionStart"      => "▶️",
            "SessionEnd"        => "⏹️",
            "SessionTerminated" => "🛑",
            "BillingGenerated"  => "💰",
            "AdminAction"       => "⚙️",
            "Error"             => "❌",
            _                   => "📋"
        };

        public string DisplayEvent => $"{EventIcon}  {EventType}";
    }

    public class LogsResponse
    {
        public bool        Success    { get; set; }
        public string      Message    { get; set; } = string.Empty;
        public List<LogDto> Logs      { get; set; } = new();
        public int         TotalCount { get; set; }
    }
}
