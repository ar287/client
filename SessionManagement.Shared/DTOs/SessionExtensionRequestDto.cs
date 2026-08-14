namespace SessionManagement.Shared.DTOs
{
    public class SessionExtensionRequestDto
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int RequestedMinutes { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    }

    public class ApproveExtensionRequest
    {
        public string RequestId { get; set; } = string.Empty;
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public int AdditionalMinutes { get; set; }
    }
}
