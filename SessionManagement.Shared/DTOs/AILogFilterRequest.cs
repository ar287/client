namespace SessionManagement.Shared.DTOs
{
    public class AILogFilterRequest
    {
        public string UserQuery { get; set; } = string.Empty;
    }

    public class AILogFilterResponse
    {
        public string NaturalQuery { get; set; } = string.Empty;
        public string? EventType { get; set; }
        public string? Username { get; set; }
        public string? IPAddress { get; set; }
        public string? SearchKeyword { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public bool IsParsed { get; set; }
    }
}
