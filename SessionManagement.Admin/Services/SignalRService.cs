using Microsoft.AspNetCore.SignalR.Client;

namespace SessionManagement.Admin.Services
{
    public class SignalRService : IAsyncDisposable
    {
        private HubConnection? _connection;
        private const string HubUrl = "http://localhost:5102/sessionhub";

        // Events the Admin UI subscribes to
        public event Action<int, string, int, int>?       OnSessionStarted;
        public event Action<int, int, string, decimal>?   OnTimerUpdated;
        public event Action<int, int, int, decimal>?      OnSessionEnded;
        public event Action<string, string, string>?      OnSecurityAlert;
        public event Action<string>?                      OnConnectionStatusChanged;

        public bool IsConnected =>
            _connection?.State == HubConnectionState.Connected;

        public async Task ConnectAsync()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(HubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Listen for new session started
            _connection.On<int, string, int, int>(
                "SessionStarted",
                (userId, fullName, sessionId, allocatedMinutes) =>
                {
                    OnSessionStarted?.Invoke(
                        userId, fullName, sessionId, allocatedMinutes);
                }
            );

            // Listen for timer updates
            _connection.On<int, int, string, decimal>(
                "TimerUpdated",
                (userId, sessionId, remainingTime, currentCost) =>
                {
                    OnTimerUpdated?.Invoke(
                        userId, sessionId, remainingTime, currentCost);
                }
            );

            // Listen for session ended
            _connection.On<int, int, int, decimal>(
                "SessionEnded",
                (userId, sessionId, totalMinutes, totalAmount) =>
                {
                    OnSessionEnded?.Invoke(
                        userId, sessionId, totalMinutes, totalAmount);
                }
            );

            // Listen for security alerts
            _connection.On<string, string, string>(
                "SecurityAlert",
                (alertType, message, severity) =>
                {
                    OnSecurityAlert?.Invoke(alertType, message, severity);
                }
            );

            // Handle reconnection
            _connection.Reconnected += async (connectionId) =>
            {
                OnConnectionStatusChanged?.Invoke("Reconnected");
                await RegisterAsAdminAsync();
            };

            _connection.Reconnecting += (exception) =>
            {
                OnConnectionStatusChanged?.Invoke("Reconnecting...");
                return Task.CompletedTask;
            };

            _connection.Closed += (exception) =>
            {
                OnConnectionStatusChanged?.Invoke("Disconnected");
                return Task.CompletedTask;
            };

            try
            {
                await _connection.StartAsync();
                await RegisterAsAdminAsync();
                OnConnectionStatusChanged?.Invoke("Connected");
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Failed: {ex.Message}");
            }
        }

        // Register this connection as Admin on the hub
        private async Task RegisterAsAdminAsync()
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync("JoinAdminGroup");
            }
        }

        // Admin terminates a customer session
        public async Task TerminateSessionAsync(int userId, string reason)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync(
                    "TerminateSession", userId, reason);
            }
        }

        // Admin sends a warning to a customer
        public async Task SendWarningAsync(int userId, string message)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync(
                    "SendWarningToCustomer", userId, message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
