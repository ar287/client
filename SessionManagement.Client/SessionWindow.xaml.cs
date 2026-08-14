using System.Windows;
using System.Windows.Threading;
using SessionManagement.Client.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Client
{
    public partial class SessionWindow : Window
    {
        private readonly ApiService     _apiService;
        private readonly SignalRService _signalRService;
        private readonly int            _userId;
        private readonly string         _fullName;
        private readonly byte[]?        _capturedImage;

        private int             _sessionId;
        private int             _allocatedMinutes;
        private DateTime        _startTime;
        private DispatcherTimer _timer         = new();
        private bool            _sessionActive = false;
        private bool            _terminating   = false;
        private int             _tickCount     = 0;
        private const decimal   RatePerMinute  = 2.00m;

        public SessionWindow(int userId, string fullName, byte[]? capturedImage = null)
        {
            InitializeComponent();
            _apiService     = new ApiService();
            _signalRService = new SignalRService();
            _userId         = userId;
            _fullName       = fullName;
            _capturedImage  = capturedImage;

            WelcomeText.Text = $"Welcome, {fullName}!";

            Loaded += async (s, e) => await ConnectSignalRAsync();
        }

        private async Task ConnectSignalRAsync()
        {
            _signalRService.OnWarningReceived       += OnWarningReceived;
            _signalRService.OnSessionTerminated     += OnSessionTerminated;
            _signalRService.OnConnectionStatusChanged += OnConnectionStatusChanged;

            await _signalRService.ConnectAsync(_userId);
        }

        // ── SignalR event handlers ──────────────────────────────────

        private void OnWarningReceived(string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"⚠  Warning from Admin:\n\n{message}",
                    "Admin Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            });
        }

        private void OnSessionTerminated(string reason)
        {
            Dispatcher.Invoke(async () =>
            {
                if (_terminating) return;
                _terminating = true;
                _timer.Stop();

                if (reason == "Terminated" || reason.Contains("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    // Admin Forced Termination: Immediate shutdown (No Continue option)
                    MessageBox.Show(
                        $"Your session has been forcibly terminated by Administrator.\n\n" +
                        $"Reason: {reason}\n\n" +
                        $"This computer will shut down shortly.",
                        "Session Terminated by Admin",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    InitiateClientShutdown();
                    await CleanupAndCloseAsync();
                }
                else
                {
                    // Automatic Expiry: Trigger Grace Period UI
                    await ShowGracePeriodDialogAsync();
                }
            });
        }

        private void OnConnectionStatusChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"[SignalR] {status}");
            });
        }

        // ── UI event handlers ───────────────────────────────────────

        private void QuickSelect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn &&
                btn.Tag is string tag)
            {
                CustomMinutesBox.Text = tag;
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide previous error
            SessionErrorBorder.Visibility = Visibility.Collapsed;

            if (!int.TryParse(CustomMinutesBox.Text.Trim(), out int minutes) || minutes < 1 || minutes > 600)
            {
                SessionErrorBorder.Visibility = Visibility.Visible;
                SessionErrorMessage.Text = "Please enter a valid duration between 1 and 600 minutes.";
                return;
            }

            StartButton.IsEnabled = false;
            StartButton.Content = "Starting session...";

            StartSessionResponse? response = await _apiService.StartSessionAsync(_userId, minutes);

            if (response == null || !response.Success)
            {
                SessionErrorBorder.Visibility = Visibility.Visible;
                SessionErrorMessage.Text = response?.Message ?? "Could not start session.";
                StartButton.IsEnabled = true;
                StartButton.Content = "Start Session";
                return;
            }

            _sessionId = response.SessionId;
            _allocatedMinutes = response.AllocatedMinutes;
            _startTime = response.StartTime;
            _sessionActive = true;

            StartTimeText.Text = _startTime.ToString("hh:mm tt");
            AllocatedText.Text = $"{_allocatedMinutes} min";

            TimeSelectionPanel.Visibility = Visibility.Collapsed;
            ActiveSessionPanel.Visibility = Visibility.Visible;

            // Notify admin session started
            await _signalRService.NotifySessionStartedAsync(_userId, _fullName, _sessionId, _allocatedMinutes);

            // Upload the captured image with the valid SessionId
            if (_capturedImage != null)
            {
                _ = Task.Run(async () =>
                {
                    await _apiService.UploadImageAsync(_userId, _sessionId, _capturedImage);
                });
            }

            // Start timer
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (_terminating) return;

            TimeSpan elapsed   = DateTime.Now - _startTime;
            TimeSpan remaining = TimeSpan.FromMinutes(_allocatedMinutes)
                                 - elapsed;

            if (remaining.TotalSeconds <= 0)
            {
                _timer.Stop();
                _terminating = true;

                TimerDisplay.Text      = "00:00:00";
                SessionStatusText.Text = "Time expired — ending session...";
                SessionStatusText.Foreground =
                    System.Windows.Media.Brushes.OrangeRed;

                await HandleAutoTerminationAsync();
                return;
            }

            // Update timer display
            TimerDisplay.Text = remaining.ToString(@"hh\:mm\:ss");

            // Update status color based on remaining time
            if (remaining.TotalMinutes <= 5)
            {
                TimerDisplay.Foreground =
                    System.Windows.Media.Brushes.OrangeRed;
                SessionStatusText.Text = "⚠  Less than 5 minutes remaining!";
                SessionStatusText.Foreground =
                    System.Windows.Media.Brushes.OrangeRed;
            }
            else
            {
                TimerDisplay.Foreground =
                    new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(26, 82, 118));
                SessionStatusText.Text = "Session is active";
                SessionStatusText.Foreground =
                    System.Windows.Media.Brushes.Green;
            }

            int     elapsedMinutes = (int)Math.Ceiling(elapsed.TotalMinutes);
            decimal currentCost    = elapsedMinutes * RatePerMinute;

            ElapsedText.Text = $"{(int)elapsed.TotalMinutes} min " +
                               $"{elapsed.Seconds:D2} sec";
            CostText.Text    = $"Rs. {currentCost:F2}";

            // Send update to admin every 30 seconds
            _tickCount++;
            if (_tickCount % 30 == 0)
            {
                await _signalRService.SendTimerUpdateAsync(
                    _userId, _sessionId,
                    remaining.ToString(@"hh\:mm\:ss"),
                    currentCost
                );
            }

            // One-time 5 minute warning popup
            if (remaining.TotalMinutes <= 5 &&
                remaining.TotalMinutes >  4.9)
            {
                MessageBox.Show(
                    "Only 5 minutes remaining in your session!\n" +
                    "Please save your work.",
                    "Time Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        // Auto termination when time expires
        private async Task HandleAutoTerminationAsync()
        {
            await ShowGracePeriodDialogAsync();
        }

        private async Task ShowGracePeriodDialogAsync()
        {
            _timer.Stop();
            var graceWindow = new GracePeriodWindow(_sessionId, _userId, _fullName, _signalRService);
            bool? result = graceWindow.ShowDialog();

            if (result == true && graceWindow.SessionContinued)
            {
                // Sync session extension with Server
                bool serverExtended = await _apiService.ExtendSessionAsync(_sessionId, graceWindow.ExtendedMinutes);
                if (serverExtended)
                {
                    _allocatedMinutes += graceWindow.ExtendedMinutes;
                    _terminating = false;
                    SessionStatusText.Text = "Session active (Extended)";
                    SessionStatusText.Foreground = System.Windows.Media.Brushes.Green;
                    TimerDisplay.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 82, 118));
                    _timer.Start();
                    return;
                }
            }

            // If not extended or grace window closed/timed out, finalize termination & shutdown
            _sessionActive = false;
            EndButton.IsEnabled = false;

            EndSessionResponse? response =
                await _apiService.TerminateSessionAsync(
                    _sessionId, "Completed");

            if (response != null && response.Success)
            {
                await _signalRService.NotifySessionEndedAsync(
                    _userId, _sessionId,
                    response.TotalMinutes,
                    response.TotalAmount
                );

                MessageBox.Show(
                    $"Your session has ended.\n\n" +
                    $"Duration : {response.TotalMinutes} minutes\n" +
                    $"Total    : Rs. {response.TotalAmount:F2}\n\n" +
                    $"Thank you for using our service!",
                    "Session Completed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                MessageBox.Show(
                    "Session ended but could not update billing. " +
                    "Please contact admin.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }

            InitiateClientShutdown();
            await CleanupAndCloseAsync();
        }

        private static void InitiateClientShutdown()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown.exe", "/s /t 10")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Shutdown] Error: {ex.Message}");
            }
        }

        // Manual end by customer
        private async void EndButton_Click(
            object sender, RoutedEventArgs e)
        {
            if (_terminating) return;

            MessageBoxResult confirm = MessageBox.Show(
                "Are you sure you want to end your session early?\n\n" +
                "You will be charged for the time used.",
                "End Session",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (confirm != MessageBoxResult.Yes) return;

            _terminating = true;
            _timer.Stop();
            _sessionActive      = false;
            EndButton.IsEnabled = false;
            EndButton.Content   = "Ending session...";

            EndSessionResponse? response =
                await _apiService.TerminateSessionAsync(
                    _sessionId, "Completed");

            if (response != null && response.Success)
            {
                await _signalRService.NotifySessionEndedAsync(
                    _userId, _sessionId,
                    response.TotalMinutes,
                    response.TotalAmount
                );

                MessageBox.Show(
                    $"Session ended successfully.\n\n" +
                    $"Duration : {response.TotalMinutes} minutes\n" +
                    $"Total    : Rs. {response.TotalAmount:F2}",
                    "Session Summary",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                MessageBox.Show(
                    response?.Message ?? "Error ending session.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            await CleanupAndCloseAsync();
        }

        // View billing history
        private void ViewBilling_Click(object sender, RoutedEventArgs e)
        {
            var billingWindow = new BillingWindow(_userId, _fullName);
            billingWindow.Owner = this;
            billingWindow.ShowDialog();
        }

        // Tab Selection Handler
        private async void MyBillsTab_Selected(object sender, RoutedEventArgs e)
        {
            await LoadMyBillingAsync();
        }

        private async Task LoadMyBillingAsync()
        {
            try
            {
                var response = await _apiService.GetMyBillingAsync(_userId);
                if (response != null && response.Success)
                {
                    BillingHistoryListView.ItemsSource = response.Records;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading billing: {ex.Message}");
            }
        }

        // Shared cleanup logic
        private async Task CleanupAndCloseAsync()
        {
            _sessionActive = false;
            _timer.Stop();
            await _signalRService.DisposeAsync();
            Dispatcher.Invoke(() => this.Close());
        }

        // Prevent accidental close while session is active
        private async void Window_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            if (_sessionActive && !_terminating)
            {
                e.Cancel = true;

                MessageBoxResult result = MessageBox.Show(
                    "You have an active session.\n\n" +
                    "Do you want to end it and close?",
                    "Session Active",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.Yes)
                {
                    _terminating = true;
                    _timer.Stop();
                    await HandleAutoTerminationAsync();
                }
            }
            else
            {
                await _signalRService.DisposeAsync();
            }
        }
    }
}
