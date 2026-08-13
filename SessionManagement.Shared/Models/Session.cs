namespace SessionManagement.Shared.Models
{
    public class Session
    {
        public int      SessionId        { get; set; }
        public int      UserId           { get; set; }
        public DateTime StartTime        { get; set; }
        public DateTime? EndTime         { get; set; }
        public int      AllocatedMinutes { get; set; }
        public int      RemainingMinutes { get; set; }
        public string   Status          { get; set; } = "Active";
        public string?  ImagePath        { get; set; }
        public string?  ClientMachine    { get; set; }
    }
}
