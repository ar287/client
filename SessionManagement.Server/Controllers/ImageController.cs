using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageController : ControllerBase
    {
        private readonly ImageService _imageService;

        public ImageController(ImageService imageService)
        {
            _imageService = imageService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage(
            [FromBody] ImageUploadRequest request)
        {
            var (success, message, imagePath) = await _imageService.SaveImageAsync(
                request.UserId,
                request.SessionId,
                request.ImageData
            );

            var response = new ImageUploadResponse
            {
                Success   = success,
                Message   = message,
                ImagePath = imagePath
            };

            return success ? Ok(response) : BadRequest(response);
        }
    }
}
