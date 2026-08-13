namespace SessionManagement.Shared.DTOs
{
    public class TerminationNotification
    {
        public int    SessionId     { get; set; }
        public int    UserId        { get; set; }
        public string Reason        { get; set; } = string.Empty;
        public string TerminatedBy  { get; set; } = string.Empty;
        public int    TotalMinutes  { get; set; }
        public decimal TotalAmount  { get; set; }
        public DateTime TerminatedAt { get; set; } = DateTime.Now;
    }
}
