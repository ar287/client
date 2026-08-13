using Microsoft.Data.SqlClient;

namespace SessionManagement.Server.Services
{
    public class ImageService
    {
        private readonly string _connectionString;
        private readonly string _capturesFolder;

        public ImageService(string connectionString, string webRootPath)
        {
            _connectionString = connectionString;

            // Build the full path to wwwroot/captures
            _capturesFolder = Path.Combine(webRootPath, "captures");

            // Create folder if it does not exist
            if (!Directory.Exists(_capturesFolder))
                Directory.CreateDirectory(_capturesFolder);
        }

        public async Task<(bool Success, string Message, string ImagePath)>
            SaveImageAsync(int userId, int sessionId, byte[] imageData)
        {
            try
            {
                if (imageData == null || imageData.Length == 0)
                    return (false, "No image data received.", string.Empty);

                // Build unique filename: userId_sessionId_timestamp.jpg
                string fileName  = $"{userId}_{sessionId}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string fullPath  = Path.Combine(_capturesFolder, fileName);
                string imagePath = $"/captures/{fileName}";

                // Save the image file to disk
                await File.WriteAllBytesAsync(fullPath, imageData);

                // Update the Sessions table with the image path
                await UpdateSessionImagePathAsync(sessionId, imagePath);

                return (true, "Image saved successfully.", imagePath);
            }
            catch (Exception ex)
            {
                return (false, $"Error saving image: {ex.Message}", string.Empty);
            }
        }

        private async Task UpdateSessionImagePathAsync(int sessionId, string imagePath)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                UPDATE Sessions
                SET    ImagePath = @ImagePath
                WHERE  SessionId = @SessionId";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ImagePath",  imagePath);
            command.Parameters.AddWithValue("@SessionId",  sessionId);

            await command.ExecuteNonQueryAsync();
        }
    }
}
