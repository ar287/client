namespace SessionManagement.Shared.DTOs
{
    public class BillingResponse
    {
        public bool                 Success  { get; set; }
        public string               Message  { get; set; } = string.Empty;
        public List<BillingRecord>  Records  { get; set; } = new();
        public decimal              Total    { get; set; }
    }
}
