using System.Windows;
using SessionManagement.Client.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Client
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService    _apiService;
        private readonly WebcamService _webcamService;

        public LoginWindow()
        {
            InitializeComponent();
            _apiService    = new ApiService();
            _webcamService = new WebcamService();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous errors
            ErrorBorder.Visibility  = Visibility.Collapsed;
            ErrorMessage.Visibility = Visibility.Collapsed;

            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            // Disable button during login
            LoginButton.IsEnabled = false;
            LoginButton.Content   = "Signing in...";

            // Step 1: Authenticate
            LoginResponse? response = await _apiService.LoginAsync(
                new LoginRequest
                {
                    Username = username,
                    Password = password
                }
            );

            if (response == null || !response.Success)
            {
                ShowError(response?.Message ?? "No response from server.");
                LoginButton.IsEnabled = true;
                LoginButton.Content   = "Sign In";
                return;
            }

            // Step 2: Capture webcam image (only for customers)
            byte[]? capturedImage = null;
            if (response.Role == "Customer")
            {
                LoginButton.Content = "Capturing image...";

                if (_webcamService.IsCameraAvailable())
                {
                    capturedImage = await _webcamService.CaptureImageAsync();
                    
                    if (capturedImage == null)
                    {
                        // Camera timed out — log but do not block login
                        MessageBox.Show(
                            "Webcam capture timed out. Login will continue.",
                            "Camera Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }
                }
                else
                {
                    // No camera — log but do not block login
                    MessageBox.Show(
                        "No webcam detected. Login will continue without image capture.",
                        "Camera Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }

            // Step 3: Navigate based on role
            LoginButton.IsEnabled = true;
            LoginButton.Content   = "Sign In";

            // Open correct window based on role
            if (response.Role == "Customer")
            {
                var sessionWindow = new SessionWindow(
                    response.UserId, 
                    response.FullName, 
                    capturedImage);
                sessionWindow.Show();
            }
            else
            {
                MessageBox.Show(
                    $"Welcome, {response.FullName}!\nAdmin panel coming in Phase 9.",
                    "Admin Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }

            this.Hide();
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text       = message;
            ErrorBorder.Visibility  = Visibility.Visible;
            ErrorMessage.Visibility = Visibility.Visible;
        }

        protected override void OnClosed(EventArgs e)
        {
            _webcamService.Dispose();
            base.OnClosed(e);
        }
    }
}
