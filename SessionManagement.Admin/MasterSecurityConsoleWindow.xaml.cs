using System;
using System.Threading.Tasks;
using System.Windows;
using SessionManagement.Admin.Services;

namespace SessionManagement.Admin
{
    public partial class MasterSecurityConsoleWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly SignalRService _signalRService;

        public MasterSecurityConsoleWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            _signalRService = new SignalRService();
            Loaded += MasterSecurityConsoleWindow_Loaded;
        }

        private async void MasterSecurityConsoleWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSystemHealthAsync();
        }

        private async Task LoadSystemHealthAsync()
        {
            StatusText.Text = "Querying system health and platform status...";
            var health = await _apiService.GetSystemHealthAsync();

            if (health != null)
            {
                DbStatusText.Text = health.DatabaseConnected ? "Connected & Active" : "Offline / Unreachable";
                DbStatusText.Foreground = health.DatabaseConnected
                    ? System.Windows.Media.Brushes.MediumSeaGreen
                    : System.Windows.Media.Brushes.IndianRed;

                UptimeText.Text = string.IsNullOrWhiteSpace(health.ServerUptime) ? "Active" : health.ServerUptime;
                AiStatusText.Text = health.AiEngineAvailable ? "llama3.2:3b Ready" : "Fallback Engine Active";

                StatusText.Text = $"System health updated at {DateTime.Now:HH:mm:ss} — {health.ActiveServices.Count} active platform microservices running.";
            }
            else
            {
                DbStatusText.Text = "Unknown Status";
                UptimeText.Text = "Server Offline";
                StatusText.Text = "Could not connect to SessionManagement.Server API.";
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadSystemHealthAsync();
        }

        private void OpenAnalytics_Click(object sender, RoutedEventArgs e)
        {
            new OperationsAnalyticsWindow().Show();
        }

        private void OpenSecurity_Click(object sender, RoutedEventArgs e)
        {
            new SecurityDashboardWindow().Show();
        }

        private void OpenAiInsights_Click(object sender, RoutedEventArgs e)
        {
            new AIInsightsDashboardWindow().Show();
        }

        private void OpenRuleBuilder_Click(object sender, RoutedEventArgs e)
        {
            new VisualRuleBuilderWindow().Show();
        }

        private void OpenLiveAlerts_Click(object sender, RoutedEventArgs e)
        {
            new LiveAlertFeedWindow(_signalRService).Show();
        }

        private void OpenApprovals_Click(object sender, RoutedEventArgs e)
        {
            new ApprovalQueueWindow().Show();
        }

        private void OpenRemediation_Click(object sender, RoutedEventArgs e)
        {
            new RemediationControlWindow().Show();
        }

        private void OpenAuditExport_Click(object sender, RoutedEventArgs e)
        {
            new AuditExportWindow().Show();
        }

        private void OpenFleetMonitor_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow().Show();
        }
    }
}
