namespace SessionManagement.Shared.DTOs
{
    public class EndSessionResponse
    {
        public bool    Success      { get; set; }
        public string  Message      { get; set; } = string.Empty;
        public int     TotalMinutes { get; set; }
        public decimal TotalAmount  { get; set; }
    }
}
