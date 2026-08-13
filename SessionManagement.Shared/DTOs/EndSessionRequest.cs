namespace SessionManagement.Shared.DTOs
{
    public class EndSessionRequest
    {
        public int    SessionId { get; set; }
        public string Reason    { get; set; } = "Completed";
    }
}
