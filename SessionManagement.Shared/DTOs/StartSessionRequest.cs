namespace SessionManagement.Shared.DTOs
{
    public class StartSessionRequest
    {
        public int UserId           { get; set; }
        public int AllocatedMinutes { get; set; }
        public string ClientMachine { get; set; } = string.Empty;
    }
}
