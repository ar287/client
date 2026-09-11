using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    // ── View model for each alert card in the feed ──────────────────────
    public class AlertCardViewModel
    {
        public string AlertId       { get; set; } = string.Empty;
        public string ClientId      { get; set; } = string.Empty;
        public string RuleName      { get; set; } = string.Empty;
        public string AlertType     { get; set; } = string.Empty;
        public string Severity      { get; set; } = string.Empty;
        public string Message       { get; set; } = string.Empty;
        public string ActionTaken   { get; set; } = string.Empty;
        public int    RiskScore     { get; set; }
        public string TimeDisplay   { get; set; } = string.Empty;

        // Derived display properties
        public string SeverityIcon => Severity switch
        {
            "Critical" => "🔴",
            "High"     => "🟠",
            "Medium"   => "🟡",
            _          => "🟢"
        };

        public string SeverityColor => Severity switch
        {
            "Critical" => "#EF4444",
            "High"     => "#F97316",
            "Medium"   => "#EAB308",
            _          => "#22C55E"
        };

        public string CardBackground => Severity switch
        {
            "Critical" => "#FEF2F2",
            "High"     => "#FFEDD5",
            "Medium"   => "#FEF9C3",
            _          => "#F0FDF4"
        };
    }

    public partial class LiveAlertFeedWindow : Window
    {
        private readonly SignalRService _signalRService;
        private readonly ObservableCollection<AlertCardViewModel> _alerts = new();

        private int _totalCount    = 0;
        private int _criticalCount = 0;
        private int _highCount     = 0;
        private int _medLowCount   = 0;

        public LiveAlertFeedWindow(SignalRService signalRService)
        {
            InitializeComponent();
            _signalRService = signalRService;
            AlertFeed.ItemsSource = _alerts;
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // Wire alert hub events
            _signalRService.OnRuleAlert += OnRuleAlertReceived;
            _signalRService.OnAlertHubStatusChanged += OnAlertHubStatusChanged;

            // Connect if not already connected
            if (!_signalRService.IsAlertConnected)
            {
                AlertHubStatus.Text = "Connecting...";
                AlertHubDot.Fill = new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08));
                await _signalRService.ConnectAlertHubAsync();
            }
            else
            {
                AlertHubStatus.Text = "Connected";
                AlertHubDot.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // Unsubscribe to avoid memory leaks — do NOT disconnect (shared service)
            _signalRService.OnRuleAlert -= OnRuleAlertReceived;
            _signalRService.OnAlertHubStatusChanged -= OnAlertHubStatusChanged;
        }

        // ── Incoming alert from server ──────────────────────────────────────

        private void OnRuleAlertReceived(SecurityAlertNotification notification)
        {
            Dispatcher.Invoke(() =>
            {
                var card = new AlertCardViewModel
                {
                    AlertId     = notification.AlertId,
                    ClientId    = $"PC: {notification.ClientId}",
                    RuleName    = notification.RuleName,
                    AlertType   = notification.AlertType,
                    Severity    = notification.Severity,
                    Message     = notification.Message,
                    ActionTaken = notification.ActionTaken,
                    RiskScore   = notification.RiskScore,
                    TimeDisplay = notification.TimestampUtc.ToLocalTime().ToString("HH:mm:ss")
                };

                // Insert newest at top
                _alerts.Insert(0, card);

                // Cap list at 200 items
                if (_alerts.Count > 200)
                    _alerts.RemoveAt(_alerts.Count - 1);

                // Update counters
                _totalCount++;
                switch (notification.Severity)
                {
                    case "Critical": _criticalCount++; break;
                    case "High":     _highCount++;     break;
                    default:         _medLowCount++;   break;
                }

                TotalCountText.Text    = _totalCount.ToString();
                CriticalCountText.Text = _criticalCount.ToString();
                HighCountText.Text     = _highCount.ToString();
                MedLowCountText.Text   = _medLowCount.ToString();
                LastAlertText.Text     = $"Last alert: {DateTime.Now:HH:mm:ss} — {notification.Severity} from {notification.ClientId}";
            });
        }

        private void OnAlertHubStatusChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                AlertHubStatus.Text = status;

                bool connected = status.Contains("Connected") && !status.Contains("Failed");
                AlertHubDot.Fill = connected
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))  // green
                    : status.Contains("Reconnecting")
                        ? new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08))  // yellow
                        : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // red
            });
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            _alerts.Clear();
            _totalCount = _criticalCount = _highCount = _medLowCount = 0;
            TotalCountText.Text = CriticalCountText.Text = HighCountText.Text = MedLowCountText.Text = "0";
            LastAlertText.Text = "Cleared. Waiting for alerts...";
        }
    }
}
