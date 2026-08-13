namespace SessionManagement.Shared.DTOs
{
    public class ImageUploadRequest
    {
        public int    UserId    { get; set; }
        public int    SessionId { get; set; }
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
    }
}
