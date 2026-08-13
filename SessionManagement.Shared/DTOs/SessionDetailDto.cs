namespace SessionManagement.Shared.DTOs
{
    public class SessionDetailDto
    {
        public int     SessionId        { get; set; }
        public int     UserId           { get; set; }
        public string  FullName         { get; set; } = string.Empty;
        public string  Username         { get; set; } = string.Empty;
        public int     AllocatedMinutes { get; set; }
        public int     RemainingMinutes { get; set; }
        public string  StartTime        { get; set; } = string.Empty;
        public string  Status           { get; set; } = string.Empty;
        public string  ClientMachine    { get; set; } = string.Empty;
        public string? ImagePath        { get; set; }
        public decimal RatePerMinute    { get; set; }
        public decimal CurrentCost      { get; set; }
        public int     ElapsedMinutes   { get; set; }
    }

    public class ActiveSessionsResponse
    {
        public bool                  Success  { get; set; }
        public string                Message  { get; set; } = string.Empty;
        public List<SessionDetailDto> Sessions { get; set; } = new();
    }
}
