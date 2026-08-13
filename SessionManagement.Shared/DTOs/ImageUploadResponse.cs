namespace SessionManagement.Shared.DTOs
{
    public class ImageUploadResponse
    {
        public bool   Success   { get; set; }
        public string Message   { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
    }
}
