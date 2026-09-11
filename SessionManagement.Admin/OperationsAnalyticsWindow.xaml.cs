using System;
using System.Threading.Tasks;
using System.Windows;
using SessionManagement.Admin.Services;

namespace SessionManagement.Admin
{
    public partial class OperationsAnalyticsWindow : Window
    {
        private readonly ApiService _apiService;

        public OperationsAnalyticsWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            Loaded += async (s, e) => await LoadAnalyticsDataAsync();
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadAnalyticsDataAsync();
        }

        private async Task LoadAnalyticsDataAsync()
        {
            try
            {
                var summary = await _apiService.GetAnalyticsSummaryAsync();
                if (summary != null)
                {
                    TotalRevenueText.Text = $"${summary.TotalRevenue:F2}";
                    TotalSessionsText.Text = summary.TotalSessions.ToString();
                    OccupancyRateText.Text = $"{summary.OccupancyPercentage:F1}%";
                    PeakHourText.Text = summary.PeakHourFormatted;

                    PcRankingGrid.ItemsSource = summary.TopPcsByRevenue;
                    HourlyGrid.ItemsSource = summary.HourlyBreakdown;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load analytics: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
