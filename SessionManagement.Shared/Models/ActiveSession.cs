namespace SessionManagement.Shared.Models
{
    public class ActiveSession
    {
        public int     UserId           { get; set; }
        public string  FullName         { get; set; } = string.Empty;
        public string  Username         { get; set; } = string.Empty;
        public int     SessionId        { get; set; }
        public int     AllocatedMinutes { get; set; }
        public string  RemainingTime    { get; set; } = "--:--:--";
        public decimal CurrentCost      { get; set; }
        public string  Status           { get; set; } = "Active";
        public string  StartedAt        { get; set; } = string.Empty;
        public string  ClientMachine    { get; set; } = string.Empty;
        public string? ImagePath        { get; set; }

        // Display helpers
        public string CostDisplay =>
            $"Rs. {CurrentCost:F2}";
        public string MachineDisplay =>
            string.IsNullOrWhiteSpace(ClientMachine)
                ? "Unknown"
                : ClientMachine;
    }
}
