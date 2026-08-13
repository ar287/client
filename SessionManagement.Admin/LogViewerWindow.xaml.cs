using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class LogViewerWindow : Window
    {
        private readonly ApiService _apiService;

        public LogViewerWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();

            // Clock
            var clock      = new DispatcherTimer();
            clock.Interval = TimeSpan.FromSeconds(1);
            clock.Tick    += (s, e) =>
            {
                ClockText.Text =
                    DateTime.Now.ToString(
                        "dddd, dd MMM yyyy  HH:mm:ss");
            };
            clock.Start();

            Loaded += async (s, e) => await LoadLogsAsync();
        }

        // ── Load Logs ─────────────────────────────────────────────────
        private async Task LoadLogsAsync(
            string? search    = null,
            string? eventType = null,
            string? dateFrom  = null,
            string? dateTo    = null)
        {
            SetStatus("Loading logs...");
            EmptyText.Text         = "Loading logs...";
            EmptyBorder.Visibility = Visibility.Visible;
            LogsListView.Visibility = Visibility.Collapsed;

            // Load stats
            var stats = await _apiService.GetLogStatsAsync();
            if (stats != null)
            {
                int logins = stats.GetValueOrDefault("Login", 0);
                int failed = stats.GetValueOrDefault("LoginFailed", 0);
                int starts = stats.GetValueOrDefault("SessionStart", 0);
                int ends   = stats.GetValueOrDefault("SessionEnd", 0);
                int terms  = stats.GetValueOrDefault(
                    "SessionTerminated", 0);

                TotalLogsText.Text   =
                    stats.Values.Sum().ToString();
                LoginLogsText.Text   = logins.ToString();
                SessionLogsText.Text =
                    (starts + ends).ToString();
                FailedLogsText.Text  = failed.ToString();
                TermLogsText.Text    = terms.ToString();
            }

            // Load logs
            var response = await _apiService.GetLogsAsync(
                search, eventType,
                dateFrom: dateFrom,
                dateTo:   dateTo,
                limit:    500);

            if (response == null || !response.Success)
            {
                EmptyText.Text = response?.Message
                    ?? "Could not load logs.";
                SetStatus("Error loading logs.");
                return;
            }

            if (response.Logs.Count == 0)
            {
                EmptyText.Text = "No logs match your filters.";
                SetStatus("No logs found.");
                return;
            }

            LogsListView.ItemsSource  = response.Logs;
            RecordCountText.Text      =
                $"{response.TotalCount} record(s)";
            EmptyBorder.Visibility    = Visibility.Collapsed;
            LogsListView.Visibility   = Visibility.Visible;

            SetStatus(
                $"{response.TotalCount} log(s) loaded.");
        }

        // ── Search ────────────────────────────────────────────────────
        private async void SearchButton_Click(
            object sender, RoutedEventArgs e)
        {
            await ApplyFiltersAsync();
        }

        private async void ResetButton_Click(
            object sender, RoutedEventArgs e)
        {
            SearchBox.Text                = string.Empty;
            EventTypeFilter.SelectedIndex = 0;
            DateFromPicker.SelectedDate   = null;
            DateToPicker.SelectedDate     = null;
            await LoadLogsAsync();
        }

        private async Task ApplyFiltersAsync()
        {
            string? search = string.IsNullOrWhiteSpace(SearchBox.Text)
                ? null
                : SearchBox.Text.Trim();

            string? eventType = EventTypeFilter.SelectedIndex switch
            {
                1 => "Login",
                2 => "Logout",
                3 => "LoginFailed",
                4 => "SessionStart",
                5 => "SessionEnd",
                6 => "SessionTerminated",
                7 => "BillingGenerated",
                _ => null
            };

            string? dateFrom = DateFromPicker.SelectedDate?
                .ToString("yyyy-MM-dd");
            string? dateTo   = DateToPicker.SelectedDate?
                .ToString("yyyy-MM-dd");

            await LoadLogsAsync(search, eventType,
                dateFrom, dateTo);
        }

        // ── AI Feature Handlers ─────────────────────────────────────
        private async void AISummary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetStatus("Generating AI Shift Brief via Ollama...");
                AISummaryBorder.Visibility = Visibility.Visible;
                AISummaryText.Text = "⏳ Reading logs and running local AI model...";

                var summary = await _apiService.GenerateAILogSummaryAsync(50);
                if (summary != null)
                {
                    AIRiskBadge.Text = $" [Risk: {summary.OperationalRisk}]";
                    AIRiskBadge.Foreground = summary.OperationalRisk switch
                    {
                        "High" => System.Windows.Media.Brushes.Red,
                        "Medium" => System.Windows.Media.Brushes.Orange,
                        _ => System.Windows.Media.Brushes.Green
                    };

                    string bulletEvents = summary.KeyEvents != null && summary.KeyEvents.Any()
                        ? "\n• " + string.Join("\n• ", summary.KeyEvents)
                        : "";

                    AISummaryText.Text = $"{summary.Summary}{bulletEvents}";
                    SetStatus("AI Shift Brief ready.");
                }
                else
                {
                    AISummaryText.Text = "⚠️ AI Summary unavailable (Ollama engine offline).";
                    SetStatus("Ollama AI offline.");
                }
            }
            catch (Exception ex)
            {
                AISummaryText.Text = "Error: " + ex.Message;
            }
        }

        private void CloseAISummary_Click(object sender, RoutedEventArgs e)
        {
            AISummaryBorder.Visibility = Visibility.Collapsed;
        }

        // ── Clear Old Logs ────────────────────────────────────────────
        private async void ClearLogs_Click(
            object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(
                "Delete all logs older than 30 days?\n\n" +
                "This action cannot be undone.",
                "Confirm Clear Logs",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (confirm != MessageBoxResult.Yes) return;

            bool success =
                await _apiService.ClearOldLogsAsync(30);

            if (success)
            {
                MessageBox.Show(
                    "Old logs cleared successfully.",
                    "Done",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                await LoadLogsAsync();
            }
            else
            {
                MessageBox.Show(
                    "Could not clear logs.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // ── Helpers ───────────────────────────────────────────────────
        private void SetStatus(string msg)
        {
            StatusText.Text =
                $"[{DateTime.Now:HH:mm:ss}]  {msg}";
        }

        private void Close_Click(
            object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
