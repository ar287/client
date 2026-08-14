using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;
using SessionManagement.Shared.Models;

namespace SessionManagement.Admin
{
    public partial class DashboardWindow : Window
    {
        private readonly ApiService      _apiService;
        private readonly SignalRService  _signalRService;
        private readonly int             _adminId;

        private ObservableCollection<ActiveSession>
            _activeSessions = new();

        private ActiveSession?    _selectedSession;
        private AlertCenterWindow? _alertCenterWindow;

        private decimal _totalRevenue  = 0;
        private int     _totalAlerts   = 0;
        private int     _sessionsToday = 0;

        // Auto refresh timer
        private DispatcherTimer _refreshTimer = new();

        public DashboardWindow(int adminId, string adminName)
        {
            InitializeComponent();
            _apiService     = new ApiService();
            _signalRService = new SignalRService();
            _adminId        = adminId;

            AdminNameText.Text = $"Logged in as: {adminName}";
            SessionsListView.ItemsSource = _activeSessions;

            // Clock
            var clock      = new DispatcherTimer();
            clock.Interval = TimeSpan.FromSeconds(1);
            clock.Tick    += (s, e) =>
            {
                CurrentTimeText.Text =
                    DateTime.Now.ToString(
                        "dddd, dd MMM yyyy   hh:mm:ss tt");
            };
            clock.Start();

            // Auto refresh every 30 seconds
            _refreshTimer.Interval = TimeSpan.FromSeconds(30);
            _refreshTimer.Tick    += async (s, e) =>
            {
                await RefreshActiveSessionsAsync();
            };
            _refreshTimer.Start();

            Loaded += async (s, e) =>
            {
                await ConnectSignalRAsync();
                await RefreshActiveSessionsAsync();
            };
        }

        // ── SignalR ──────────────────────────────────────────────────

        private async Task ConnectSignalRAsync()
        {
            _signalRService.OnSessionStarted   += OnSessionStarted;
            _signalRService.OnTimerUpdated     += OnTimerUpdated;
            _signalRService.OnSessionEnded     += OnSessionEnded;
            _signalRService.OnSecurityAlert    += OnSecurityAlert;
            _signalRService.OnExtensionRequested += OnExtensionRequested;
            _signalRService.OnConnectionStatusChanged += OnConnectionChanged;

            await _signalRService.ConnectAsync();
        }

        // ── Refresh Active Sessions from DB ──────────────────────────

        private async Task RefreshActiveSessionsAsync()
        {
            var response =
                await _apiService.GetActiveSessionsAsync();

            if (response == null || !response.Success) return;

            Dispatcher.Invoke(() =>
            {
                _activeSessions.Clear();

                foreach (var s in response.Sessions)
                {
                    _activeSessions.Add(new ActiveSession
                    {
                        UserId           = s.UserId,
                        FullName         = s.FullName,
                        Username         = s.Username,
                        SessionId        = s.SessionId,
                        AllocatedMinutes = s.AllocatedMinutes,
                        RemainingTime    = FormatMinutes(
                                            s.RemainingMinutes),
                        CurrentCost      = s.CurrentCost,
                        Status           = s.Status,
                        StartedAt        = s.StartTime,
                        ClientMachine    = s.ClientMachine,
                        ImagePath        = s.ImagePath
                    });
                }

                ActiveSessionsCount.Text =
                    _activeSessions.Count.ToString();

                LastRefreshText.Text =
                    $"Last refreshed: " +
                    $"{DateTime.Now:hh:mm:ss tt}";
            });
        }

        private string FormatMinutes(int minutes)
        {
            int h = minutes / 60;
            int m = minutes % 60;
            return $"{h:D2}:{m:D2}:00";
        }

        // ── SignalR Event Handlers ───────────────────────────────────

        private void OnSessionStarted(
            int userId, string fullName,
            int sessionId, int allocatedMinutes)
        {
            Dispatcher.Invoke(async () =>
            {
                // Refresh from DB to get complete data
                await RefreshActiveSessionsAsync();

                _sessionsToday++;
                SessionsTodayCount.Text = _sessionsToday.ToString();

                AddAlert(
                    $"▶️  Session started — " +
                    $"{fullName} (Session #{sessionId})");

                SetStatus(
                    $"New session: {fullName} — " +
                    $"{allocatedMinutes} min");
            });
        }

        private void OnTimerUpdated(
            int userId, int sessionId,
            string remainingTime, decimal currentCost)
        {
            Dispatcher.Invoke(() =>
            {
                var session = _activeSessions
                    .FirstOrDefault(
                        s => s.SessionId == sessionId);

                if (session == null) return;

                session.RemainingTime = remainingTime;
                session.CurrentCost   = currentCost;

                // Force ListView refresh
                RefreshListView();
                UpdateTotalRevenue();

                // Update detail panel if this session selected
                if (_selectedSession?.SessionId == sessionId)
                {
                    DetailCost.Text =
                        $"Rs. {currentCost:F2}";
                }
            });
        }

        private void OnSessionEnded(
            int userId, int sessionId,
            int totalMinutes, decimal totalAmount)
        {
            Dispatcher.Invoke(async () =>
            {
                var session = _activeSessions
                    .FirstOrDefault(
                        s => s.SessionId == sessionId);

                if (session != null)
                {
                    _activeSessions.Remove(session);
                    _totalRevenue += totalAmount;

                    ActiveSessionsCount.Text =
                        _activeSessions.Count.ToString();
                    TotalRevenueText.Text =
                        $"Rs. {_totalRevenue:F2}";

                    AddAlert(
                        $"⏹️  Session ended — " +
                        $"User #{userId} | " +
                        $"{totalMinutes} min | " +
                        $"Rs. {totalAmount:F2}");

                    SetStatus(
                        $"Session #{sessionId} ended. " +
                        $"Amount: Rs. {totalAmount:F2}");
                }

                if (_selectedSession?.SessionId == sessionId)
                {
                    _selectedSession = null;
                    ClearDetailPanel();
                }

                await RefreshActiveSessionsAsync();
            });
        }

        private void OnConnectionChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                ConnectionStatus.Text = status;
                ConnectionDot.Fill    =
                    status == "Connected"
                        ? Brushes.LimeGreen
                        : Brushes.OrangeRed;

                SetStatus($"Connection: {status}");
            });
        }

        private void OnSecurityAlert(
            string alertType,
            string message,
            string severity)
        {
            Dispatcher.Invoke(() =>
            {
                if (_alertCenterWindow != null &&
                    _alertCenterWindow.IsLoaded)
                {
                    _alertCenterWindow.AddLiveAlert(new SecurityAlert
                    {
                        AlertType   = alertType,
                        Description = message,
                        Severity    = severity,
                        CreatedAt   = DateTime.Now.ToString("F"),
                        IsRead      = false
                    });
                }

                if (severity == "High")
                {
                    MessageBox.Show(
                        $"⚠  Security Alert\n\n" +
                        $"Type     : {alertType}\n" +
                        $"Severity : {severity}\n\n" +
                        $"{message}",
                        "Security Alert",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }

                AddAlert(
                    $"[{severity}] {alertType} — {message}");
                SetStatus(
                    $"Security alert: {alertType} ({severity})");
            });
        }

        private void OnExtensionRequested(
            string requestId, int sessionId, int userId, string customerName, int minutes, decimal amount)
        {
            Dispatcher.Invoke(async () =>
            {
                AddAlert($"💳 EXTENSION REQUEST — {customerName} (Session #{sessionId}) | +{minutes} min | Amount: Rs. {amount:F2}");
                SetStatus($"Extension request from {customerName} (+{minutes}m)");

                MessageBoxResult result = MessageBox.Show(
                    $"Customer '{customerName}' is requesting a session extension:\n\n" +
                    $"➕ Additional Time: {minutes} minutes\n" +
                    $"💵 Amount Due: Rs. {amount:F2}\n\n" +
                    $"Has the customer paid cash / confirmed payment?",
                    "Confirm Payment & Approve Extension",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    // Approve extension on server and notify customer
                    bool apiSuccess = await _apiService.ExtendSessionAsync(sessionId, minutes);
                    if (apiSuccess)
                    {
                        await _signalRService.ApproveExtensionAsync(requestId, sessionId, userId, minutes);
                        AddAlert($"✅ APPROVED — Extension for {customerName} (+{minutes}m)");
                        await RefreshActiveSessionsAsync();
                    }
                    else
                    {
                        MessageBox.Show("Failed to extend session in database.", "Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    await _signalRService.RejectExtensionAsync(requestId, sessionId, userId, "Payment not confirmed by Admin.");
                    AddAlert($"❌ REJECTED — Extension for {customerName}");
                }
            });
        }

        // ── Session Selection & Detail Panel ────────────────────────

        private async void SessionsListView_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedSession =
                SessionsListView.SelectedItem as ActiveSession;

            if (_selectedSession == null)
            {
                ClearDetailPanel();
                SelectedUserText.Text       = "None selected";
                SendWarningButton.IsEnabled = false;
                TerminateButton.IsEnabled   = false;
                return;
            }

            SelectedUserText.Text =
                $"{_selectedSession.FullName} " +
                $"(Session #{_selectedSession.SessionId})";
            SendWarningButton.IsEnabled = true;
            TerminateButton.IsEnabled   = true;

            await LoadSessionDetailAsync(_selectedSession);
        }

        private async Task LoadSessionDetailAsync(
            ActiveSession session)
        {
            NoSelectionPanel.Visibility  = Visibility.Collapsed;
            DetailContentPanel.Visibility = Visibility.Visible;

            // Get fresh data from server
            var detail = await _apiService
                .GetSessionDetailAsync(session.SessionId);

            if (detail == null) return;

            // User info
            DetailFullName.Text =
                detail.FullName;
            DetailUsername.Text =
                $"@{detail.Username}";

            // Stats
            DetailStartTime.Text =
                detail.StartTime;
            DetailMachine.Text   =
                detail.ClientMachine;
            DetailAllocated.Text =
                $"{detail.AllocatedMinutes} min";
            DetailElapsed.Text   =
                $"{detail.ElapsedMinutes} min";
            DetailCost.Text      =
                $"Rs. {detail.CurrentCost:F2}";

            // Load webcam image
            await LoadSessionImageAsync(detail.ImagePath);
        }

        private async Task LoadSessionImageAsync(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                SessionImage.Source    = null;
                SessionImage.Visibility = Visibility.Collapsed;
                NoImagePanel.Visibility = Visibility.Visible;
                NoImageText.Text        = "No image captured";
                ImageBadge.Visibility   = Visibility.Collapsed;
                return;
            }

            try
            {
                byte[]? imageBytes =
                    await _apiService.GetSessionImageAsync(
                        imagePath);

                if (imageBytes == null || imageBytes.Length == 0)
                {
                    SessionImage.Source     = null;
                    SessionImage.Visibility = Visibility.Collapsed;
                    NoImagePanel.Visibility = Visibility.Visible;
                    NoImageText.Text        = "Image not found";
                    ImageBadge.Visibility   = Visibility.Collapsed;
                    return;
                }

                using var stream = new MemoryStream(imageBytes);
                var bitmap       = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource     = stream;
                bitmap.CacheOption      =
                    BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                SessionImage.Source     = bitmap;
                SessionImage.Visibility = Visibility.Visible;
                NoImagePanel.Visibility = Visibility.Collapsed;
                ImageBadge.Visibility   = Visibility.Visible;
            }
            catch
            {
                SessionImage.Source     = null;
                SessionImage.Visibility = Visibility.Collapsed;
                NoImagePanel.Visibility = Visibility.Visible;
                NoImageText.Text        = "Could not load image";
                ImageBadge.Visibility   = Visibility.Collapsed;
            }
        }

        private void ClearDetailPanel()
        {
            NoSelectionPanel.Visibility   = Visibility.Visible;
            DetailContentPanel.Visibility = Visibility.Collapsed;
            SessionImage.Source           = null;
            SessionImage.Visibility       = Visibility.Collapsed;
            NoImagePanel.Visibility       = Visibility.Visible;
            ImageBadge.Visibility         = Visibility.Collapsed;
        }

        // ── Actions ──────────────────────────────────────────────────

        private async void SendWarning_Click(
            object sender, RoutedEventArgs e)
        {
            if (_selectedSession == null) return;

            string message = WarningMessageBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show(
                    "Please enter a warning message.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            await _signalRService.SendWarningAsync(
                _selectedSession.UserId, message);

            AddAlert(
                $"📢  Warning sent to " +
                $"{_selectedSession.FullName}: {message}");
            SetStatus(
                $"Warning sent to {_selectedSession.FullName}");

            MessageBox.Show(
                $"Warning sent to {_selectedSession.FullName}.",
                "Warning Sent",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private async void TerminateSession_Click(
            object sender, RoutedEventArgs e)
        {
            if (_selectedSession == null) return;

            MessageBoxResult confirm = MessageBox.Show(
                $"Force terminate session for:\n\n" +
                $"👤  {_selectedSession.FullName}\n" +
                $"#   Session #{_selectedSession.SessionId}\n\n" +
                $"This will end the session immediately.",
                "Confirm Termination",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (confirm != MessageBoxResult.Yes) return;

            TerminateButton.IsEnabled = false;
            TerminateButton.Content   = "Terminating...";

            var response =
                await _apiService.TerminateSessionAsync(
                    _selectedSession.SessionId, "Terminated");

            TerminateButton.IsEnabled = true;
            TerminateButton.Content   = "Force Terminate Session";

            if (response != null && response.Success)
            {
                AddAlert(
                    $"🛑  TERMINATED — " +
                    $"{_selectedSession.FullName} " +
                    $"(Session #{_selectedSession.SessionId}) | " +
                    $"{response.TotalMinutes} min | " +
                    $"Rs. {response.TotalAmount:F2}");

                SetStatus(
                    $"Session #{_selectedSession.SessionId} " +
                    $"terminated.");

                MessageBox.Show(
                    $"Session terminated.\n\n" +
                    $"User     : {_selectedSession.FullName}\n" +
                    $"Duration : {response.TotalMinutes} min\n" +
                    $"Charged  : Rs. {response.TotalAmount:F2}",
                    "Terminated",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                await RefreshActiveSessionsAsync();
                ClearDetailPanel();
            }
        }

        // ── Navigation Buttons ───────────────────────────────────────

        private void ViewBilling_Click(
            object sender, RoutedEventArgs e)
        {
            var w = new BillingOverviewWindow();
            w.Owner = this;
            w.ShowDialog();
        }

        private void ViewCustomers_Click(
            object sender, RoutedEventArgs e)
        {
            var w = new CustomersWindow();
            w.Owner = this;
            w.ShowDialog();
        }

        private void ViewLogs_Click(
            object sender, RoutedEventArgs e)
        {
            var w = new LogViewerWindow();
            w.Owner = this;
            w.ShowDialog();
        }

        private void ViewSecurityAlerts_Click(
            object sender, RoutedEventArgs e)
        {
            if (_alertCenterWindow == null ||
                !_alertCenterWindow.IsLoaded)
            {
                _alertCenterWindow =
                    new AlertCenterWindow();
                _alertCenterWindow.Owner = this;
            }

            _alertCenterWindow.Show();
            _alertCenterWindow.Focus();
        }

        // ── Helpers ──────────────────────────────────────────────────

        private void AddAlert(string message)
        {
            string ts =
                $"[{DateTime.Now:HH:mm:ss}] {message}";
            AlertsListBox.Items.Insert(0, ts);
            _totalAlerts++;
            TotalAlertsCount.Text = _totalAlerts.ToString();

            if (AlertsListBox.Items.Count > 60)
                AlertsListBox.Items.RemoveAt(
                    AlertsListBox.Items.Count - 1);
        }

        private void SetStatus(string message)
        {
            StatusBarText.Text =
                $"[{DateTime.Now:HH:mm:ss}] {message}";
        }

        private void RefreshListView()
        {
            var temp = SessionsListView.ItemsSource;
            SessionsListView.ItemsSource = null;
            SessionsListView.ItemsSource = temp;
        }

        private void UpdateTotalRevenue()
        {
            decimal active =
                _activeSessions.Sum(s => s.CurrentCost);
            TotalRevenueText.Text =
                $"Rs. {(_totalRevenue + active):F2}";
        }

        private async void Window_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            _refreshTimer.Stop();
            await _signalRService.DisposeAsync();
        }
    }
}
