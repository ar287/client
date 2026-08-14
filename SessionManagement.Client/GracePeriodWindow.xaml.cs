using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using SessionManagement.Client.Services;

namespace SessionManagement.Client
{
    public partial class GracePeriodWindow : Window
    {
        private readonly int _sessionId;
        private readonly int _userId;
        private readonly string _customerName;
        private readonly SignalRService _signalRService;

        private DispatcherTimer _timer = new();
        private int _remainingSeconds = 60;
        private bool _isActionTaken = false;
        private string? _currentRequestId;

        public bool SessionContinued { get; private set; } = false;
        public int ExtendedMinutes { get; private set; } = 0;

        public GracePeriodWindow(int sessionId, int userId, string customerName, SignalRService signalRService)
        {
            InitializeComponent();
            _sessionId = sessionId;
            _userId = userId;
            _customerName = customerName;
            _signalRService = signalRService;

            _signalRService.OnExtensionApproved += OnExtensionApproved;
            _signalRService.OnExtensionRejected += OnExtensionRejected;

            StartCountdown();
        }

        private void StartCountdown()
        {
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_isActionTaken) return;

            _remainingSeconds--;
            CountdownText.Text = $"{_remainingSeconds}s";

            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                StatusMessageText.Text = "Grace period expired! Shutting down...";
                InitiateShutdown();
            }
        }

        private async void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isActionTaken) return;

            int minutes = 15;
            if (Radio30.IsChecked == true) minutes = 30;
            else if (Radio60.IsChecked == true) minutes = 60;

            decimal amount = minutes * 2.00m;

            _currentRequestId = Guid.NewGuid().ToString();
            StatusMessageText.Text = $"Extension request sent to Admin (+{minutes}m, Rs. {amount:F2}). Waiting for payment confirmation...";
            StatusMessageText.Foreground = System.Windows.Media.Brushes.Yellow;

            ContinueButton.IsEnabled = false;
            EndButton.IsEnabled = false;
            ExtensionOptionsPanel.IsEnabled = false;

            try
            {
                await _signalRService.RequestExtensionAsync(_currentRequestId, _sessionId, _userId, _customerName, minutes, amount);
            }
            catch (Exception ex)
            {
                StatusMessageText.Text = $"Failed to send request to Admin: {ex.Message}";
                ContinueButton.IsEnabled = true;
                EndButton.IsEnabled = true;
                ExtensionOptionsPanel.IsEnabled = true;
            }
        }

        private void OnExtensionApproved(string requestId, int sessionId, int minutes)
        {
            if (_isActionTaken || sessionId != _sessionId) return;

            Dispatcher.Invoke(() =>
            {
                _isActionTaken = true;
                _timer.Stop();
                SessionContinued = true;
                ExtendedMinutes = minutes;

                MessageBox.Show(
                    $"Payment confirmed by Admin!\nSession extended by {minutes} minutes.",
                    "Extension Approved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                CleanupAndClose();
            });
        }

        private void OnExtensionRejected(string requestId, int sessionId, string reason)
        {
            if (_isActionTaken || sessionId != _sessionId) return;

            Dispatcher.Invoke(() =>
            {
                StatusMessageText.Text = $"Extension Request Rejected: {reason}";
                StatusMessageText.Foreground = System.Windows.Media.Brushes.OrangeRed;

                ContinueButton.IsEnabled = true;
                EndButton.IsEnabled = true;
                ExtensionOptionsPanel.IsEnabled = true;
            });
        }

        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            InitiateShutdown();
        }

        private void InitiateShutdown()
        {
            if (_isActionTaken) return;
            _isActionTaken = true;
            _timer.Stop();

            StatusMessageText.Text = "Session ended. This computer will shut down shortly.";
            StatusMessageText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            ContinueButton.IsEnabled = false;
            EndButton.IsEnabled = false;

            try
            {
                Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 10")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Shutdown] Error: {ex.Message}");
            }

            Task.Delay(2000).ContinueWith(_ => Dispatcher.Invoke(CleanupAndClose));
        }

        private void CleanupAndClose()
        {
            _signalRService.OnExtensionApproved -= OnExtensionApproved;
            _signalRService.OnExtensionRejected -= OnExtensionRejected;
            DialogResult = SessionContinued;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            _signalRService.OnExtensionApproved -= OnExtensionApproved;
            _signalRService.OnExtensionRejected -= OnExtensionRejected;
            base.OnClosed(e);
        }
    }
}
