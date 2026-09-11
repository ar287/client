using Microsoft.AspNetCore.SignalR.Client;
using SessionManagement.Shared;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin.Services
{
    public class SignalRService : IAsyncDisposable
    {
        private HubConnection? _connection;
        private HubConnection? _alertConnection;

        // ── Existing Session Hub Events ─────────────────────────────────────
        public event Action<int, string, int, int>?       OnSessionStarted;
        public event Action<int, int, string, decimal>?   OnTimerUpdated;
        public event Action<int, int, int, decimal>?      OnSessionEnded;
        public event Action<string, string, string>?      OnSecurityAlert;
        public event Action<string>?                      OnConnectionStatusChanged;
        public event Action<string, int, int, string, int, decimal>? OnExtensionRequested;

        // ── Phase 8: Alert Hub Events ───────────────────────────────────────
        /// <summary>Fires when the server's Rule Engine pushes a live security alert.</summary>
        public event Action<SecurityAlertNotification>?   OnRuleAlert;
        public event Action<string>?                      OnAlertHubStatusChanged;

        public bool IsConnected =>
            _connection?.State == HubConnectionState.Connected;

        public bool IsAlertConnected =>
            _alertConnection?.State == HubConnectionState.Connected;

        // ── Connect to Session Hub (existing) ───────────────────────────────
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
                    OnSessionStarted?.Invoke(userId, fullName, sessionId, allocatedMinutes);
                }
            );

            // Listen for timer updates
            _connection.On<int, int, string, decimal>(
                "TimerUpdated",
                (userId, sessionId, remainingTime, currentCost) =>
                {
                    OnTimerUpdated?.Invoke(userId, sessionId, remainingTime, currentCost);
                }
            );

            // Listen for session ended
            _connection.On<int, int, int, decimal>(
                "SessionEnded",
                (userId, sessionId, totalMinutes, totalAmount) =>
                {
                    OnSessionEnded?.Invoke(userId, sessionId, totalMinutes, totalAmount);
                }
            );

            // Listen for generic security alerts (legacy)
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

        // ── Connect to Alert Hub (Phase 8) ──────────────────────────────────
        public async Task ConnectAlertHubAsync()
        {
            _alertConnection = new HubConnectionBuilder()
                .WithUrl(AppConfig.AlertHubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Receive typed RuleAlert from server's Rule Engine
            _alertConnection.On<SecurityAlertNotification>(
                "RuleAlert",
                (notification) =>
                {
                    OnRuleAlert?.Invoke(notification);
                }
            );

            _alertConnection.Reconnected += async (_) =>
            {
                OnAlertHubStatusChanged?.Invoke("AlertHub Reconnected");
                await RegisterAsAlertAdminAsync();
            };

            _alertConnection.Reconnecting += (_) =>
            {
                OnAlertHubStatusChanged?.Invoke("AlertHub Reconnecting...");
                return Task.CompletedTask;
            };

            _alertConnection.Closed += (_) =>
            {
                OnAlertHubStatusChanged?.Invoke("AlertHub Disconnected");
                return Task.CompletedTask;
            };

            try
            {
                await _alertConnection.StartAsync();
                await RegisterAsAlertAdminAsync();
                OnAlertHubStatusChanged?.Invoke("AlertHub Connected");
            }
            catch (Exception ex)
            {
                OnAlertHubStatusChanged?.Invoke($"AlertHub Failed: {ex.Message}");
            }
        }

        // ── Register as admin on SessionHub ─────────────────────────────────
        private async Task RegisterAsAdminAsync()
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync("JoinAdminGroup");
            }
        }

        // ── Register as admin on AlertHub ────────────────────────────────────
        private async Task RegisterAsAlertAdminAsync()
        {
            if (_alertConnection != null && IsAlertConnected)
            {
                await _alertConnection.InvokeAsync("JoinAlertGroup");
            }
        }

        // ── Admin terminates a customer session ──────────────────────────────
        public async Task TerminateSessionAsync(int userId, string reason)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync("TerminateSession", userId, reason);
            }
        }

        // ── Admin sends a warning to a customer ─────────────────────────────
        public async Task SendWarningAsync(int userId, string message)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync("SendWarningToCustomer", userId, message);
            }
        }

        // ── Admin approves session extension ────────────────────────────────
        public async Task ApproveExtensionAsync(string requestId, int sessionId, int userId, int minutes)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync("ApproveExtension", requestId, sessionId, userId, minutes);
            }
        }

        // ── Admin rejects session extension ─────────────────────────────────
        public async Task RejectExtensionAsync(string requestId, int sessionId, int userId, string reason)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync("RejectExtension", requestId, sessionId, userId, reason);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
                await _connection.DisposeAsync();

            if (_alertConnection != null)
                await _alertConnection.DisposeAsync();
        }
    }
}
