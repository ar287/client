using Microsoft.AspNetCore.SignalR.Client;
using SessionManagement.Shared;

namespace SessionManagement.Admin.Services
{
    public class SignalRService : IAsyncDisposable
    {
        private HubConnection? _connection;

        // Events the Admin UI subscribes to
        public event Action<int, string, int, int>?       OnSessionStarted;
        public event Action<int, int, string, decimal>?   OnTimerUpdated;
        public event Action<int, int, int, decimal>?      OnSessionEnded;
        public event Action<string, string, string>?      OnSecurityAlert;
        public event Action<string>?                      OnConnectionStatusChanged;
        public event Action<string, int, int, string, int, decimal>? OnExtensionRequested;

        public bool IsConnected =>
            _connection?.State == HubConnectionState.Connected;

        public async Task ConnectAsync()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(AppConfig.HubUrl)
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

            // Listen for session extension requests from customers
            _connection.On<string, int, int, string, int, decimal>(
                "ExtensionRequested",
                (requestId, sessionId, userId, customerName, minutes, amount) =>
                {
                    OnExtensionRequested?.Invoke(requestId, sessionId, userId, customerName, minutes, amount);
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

        // Admin approves session extension
        public async Task ApproveExtensionAsync(string requestId, int sessionId, int userId, int minutes)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync(
                    "ApproveExtension", requestId, sessionId, userId, minutes);
            }
        }

        // Admin rejects session extension
        public async Task RejectExtensionAsync(string requestId, int sessionId, int userId, string reason)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync(
                    "RejectExtension", requestId, sessionId, userId, reason);
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
