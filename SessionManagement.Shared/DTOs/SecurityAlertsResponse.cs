namespace SessionManagement.Shared.DTOs
{
    public class SecurityAlertsResponse
    {
        public bool                  Success { get; set; }
        public string                Message { get; set; } = string.Empty;
        public List<SecurityAlert>   Alerts  { get; set; } = new();
        public int                   UnreadCount { get; set; }
    }
}
