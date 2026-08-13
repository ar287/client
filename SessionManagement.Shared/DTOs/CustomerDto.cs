namespace SessionManagement.Shared.DTOs
{
    public class CustomerDto
    {
        public int    UserId    { get; set; }
        public string FullName  { get; set; } = string.Empty;
        public string Username  { get; set; } = string.Empty;
        public bool   IsActive  { get; set; }
        public string CreatedAt { get; set; } = string.Empty;

        // Stats
        public int     TotalSessions { get; set; }
        public decimal TotalSpent    { get; set; }
        public string  LastSeen      { get; set; } = "Never";

        // Display helpers
        public string StatusDisplay  =>
            IsActive ? "✅  Active" : "❌  Inactive";
        public string SpentDisplay   =>
            $"Rs. {TotalSpent:F2}";
        public string SessionsDisplay =>
            TotalSessions.ToString();
    }

    public class CustomerListResponse
    {
        public bool              Success   { get; set; }
        public string            Message   { get; set; } = string.Empty;
        public List<CustomerDto> Customers { get; set; } = new();
        public int               Total     { get; set; }
        public int               Active    { get; set; }
        public int               Inactive  { get; set; }
    }

    public class CreateCustomerRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateCustomerRequest
    {
        public int    UserId   { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CustomerActionResponse
    {
        public bool   Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
