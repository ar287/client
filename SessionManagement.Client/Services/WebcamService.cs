using AForge.Video;
using AForge.Video.DirectShow;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SessionManagement.Client.Services
{
    public class WebcamService : IDisposable
    {
        private VideoCaptureDevice?      _videoDevice;
        private FilterInfoCollection?    _videoDevices;
        private TaskCompletionSource<byte[]>? _captureTask;
        private bool                     _disposed = false;

        // Check if any webcam is available on this machine
        public bool IsCameraAvailable()
        {
            _videoDevices = new FilterInfoCollection(
                FilterCategory.VideoInputDevice
            );
            return _videoDevices.Count > 0;
        }

        // Capture one frame and return it as a byte array (JPEG)
        public async Task<byte[]?> CaptureImageAsync()
        {
            if (!IsCameraAvailable())
                return null;

            _captureTask = new TaskCompletionSource<byte[]>();

            // Use the first available camera
            _videoDevice = new VideoCaptureDevice(
                _videoDevices![0].MonikerString
            );

            _videoDevice.NewFrame += OnNewFrame;
            _videoDevice.Start();

            // Wait for one frame — timeout after 8 seconds
            var timeoutTask = Task.Delay(8000);
            var completedTask = await Task.WhenAny(
                _captureTask.Task,
                timeoutTask
            );

            // Always stop the camera after capture
            StopCamera();

            if (completedTask == timeoutTask)
                return null; // Camera timed out

            return await _captureTask.Task;
        }

        private void OnNewFrame(object sender, NewFrameEventArgs e)
        {
            // Only capture the very first frame
            if (_captureTask == null || _captureTask.Task.IsCompleted)
                return;

            try
            {
                // Clone the bitmap before the camera releases it
                using Bitmap frameCopy = (Bitmap)e.Frame.Clone();
                using MemoryStream ms   = new MemoryStream();

                frameCopy.Save(ms, ImageFormat.Jpeg);
                byte[] imageBytes = ms.ToArray();

                _captureTask.TrySetResult(imageBytes);
            }
            catch (Exception ex)
            {
                _captureTask?.TrySetException(ex);
            }
        }

        private void StopCamera()
        {
            if (_videoDevice != null && _videoDevice.IsRunning)
            {
                _videoDevice.SignalToStop();
                _videoDevice.NewFrame -= OnNewFrame;
                _videoDevice = null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopCamera();
                _disposed = true;
            }
        }
    }
}
