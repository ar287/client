namespace SessionManagement.Shared.DTOs
{
    public class StartSessionResponse
    {
        public bool   Success          { get; set; }
        public string Message          { get; set; } = string.Empty;
        public int    SessionId        { get; set; }
        public int    AllocatedMinutes { get; set; }
        public DateTime StartTime      { get; set; }
    }
}
