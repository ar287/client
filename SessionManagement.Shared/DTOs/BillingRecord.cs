namespace SessionManagement.Shared.DTOs
{
    public class BillingRecord
    {
        public int     BillingId     { get; set; }
        public int     SessionId     { get; set; }
        public int     UserId        { get; set; }
        public string  FullName      { get; set; } = string.Empty;
        public decimal RatePerMinute { get; set; }
        public int     TotalMinutes  { get; set; }
        public decimal TotalAmount   { get; set; }
        public bool    IsPaid        { get; set; }
        public string  GeneratedAt   { get; set; } = string.Empty;
        public string  SessionStatus { get; set; } = string.Empty;
 
        // UI Helpers
        public string TotalMinutesDisplay => $"{TotalMinutes} min";
        public string RateDisplay         => $"Rs. {RatePerMinute:F2}";
        public string TotalAmountDisplay  => $"Rs. {TotalAmount:F2}";
        public string PaidDisplay         => IsPaid ? "Paid" : "Pending";
    }
}
