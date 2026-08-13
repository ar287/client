using System.Windows;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService;

        public LoginWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            this.MouseDown += (s, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove(); };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorBorder.Visibility = Visibility.Collapsed;

            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            LoginButton.IsEnabled = false;
            LoginButton.Content   = "Signing in...";

            LoginResponse? response = await _apiService.LoginAsync(
                new LoginRequest
                {
                    Username = username,
                    Password = password
                }
            );

            LoginButton.IsEnabled = true;
            LoginButton.Content   = "Sign In as Admin";

            if (response == null || !response.Success)
            {
                ShowError(response?.Message ?? "No response from server.");
                return;
            }

            // Only allow Admin role
            if (response.Role != "Admin")
            {
                ShowError("Access denied. Admin credentials required.");
                return;
            }

            // Open the dashboard
            var dashboard = new DashboardWindow(
                response.UserId,
                response.FullName
            );
            dashboard.Show();
            this.Hide();
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text      = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }
    }
}
