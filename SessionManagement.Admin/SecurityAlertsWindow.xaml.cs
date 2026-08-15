using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using SessionManagement.Shared;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class SecurityAlertsWindow : Window
    {
        private readonly HttpClient _httpClient;

        public SecurityAlertsWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(AppConfig.BaseUrl)
            };
            Loaded += async (s, e) => await LoadAlertsAsync();
        }

        private async Task LoadAlertsAsync()
        {
            // MessageText.Text          = "Loading alerts...";
            // MessageBorder.Visibility  = Visibility.Visible;
            AlertsListView.Visibility = Visibility.Collapsed;
            // RefreshButton.IsEnabled   = false;
            // MarkReadButton.IsEnabled  = false;

            try
            {
                var response = await _httpClient
                    .GetFromJsonAsync<SecurityAlertsResponse>(
                        "/api/security/alerts?limit=100");

                // RefreshButton.IsEnabled   = true;
                // MarkReadButton.IsEnabled = true;

                if (response == null || !response.Success)
                {
                    // MessageText.Text = "Could not load alerts.";
                    return;
                }

                if (response.Alerts.Count == 0)
                {
                    // MessageText.Text = "No security alerts found.";
                    return;
                }

                // Summary cards
                TotalAlertsText.Text  = response.Alerts.Count.ToString();
                // HighAlertsText.Text   = response.Alerts.Count(a => a.Severity == "High").ToString();
                UnreadAlertsText.Text = response.UnreadCount.ToString();
                
                // Last Incident
                if (response.Alerts.Any())
                    LastIncidentText.Text = response.Alerts.First().AlertType;

                AlertsListView.ItemsSource = response.Alerts;

                // MessageBorder.Visibility   = Visibility.Collapsed;
                AlertsListView.Visibility  = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // RefreshButton.IsEnabled  = true;
                // MarkReadButton.IsEnabled = true;
                // MessageText.Text = $"Error: {ex.Message}";
            }
        }

        private async void Refresh_Click(
            object sender, RoutedEventArgs e)
        {
            await LoadAlertsAsync();
        }

        private async void MarkAllRead_Click(
            object sender, RoutedEventArgs e)
        {
            try
            {
                await _httpClient.PutAsync(
                    "/api/security/markread", null);
                await LoadAlertsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not mark alerts as read: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void Close_Click(
            object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
