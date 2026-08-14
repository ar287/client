using Microsoft.AspNetCore.SignalR.Client;

namespace SessionManagement.Client.Services
{
    public class SignalRService : IAsyncDisposable
    {
        private HubConnection? _connection;
        private const string HubUrl = "http://localhost:5102/sessionhub";

        // Events that the UI can subscribe to
        public event Action<string>?  OnWarningReceived;
        public event Action<string>?  OnSessionTerminated;
        public event Action<string>?  OnConnectionStatusChanged;
        public event Action<string, int, int>? OnExtensionApproved;
        public event Action<string, int, string>? OnExtensionRejected;

        public bool IsConnected =>
            _connection?.State == HubConnectionState.Connected;

        public async Task ConnectAsync(int userId)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(HubUrl)
                .WithAutomaticReconnect()
                .Build();

            // Handle incoming warning from admin
            _connection.On<string>("WarningReceived", (message) =>
            {
                OnWarningReceived?.Invoke(message);
            });

            // Handle session termination by admin
            _connection.On<string>("SessionTerminated", (reason) =>
            {
                OnSessionTerminated?.Invoke(reason);
            });

            // Handle extension approved
            _connection.On<string, int, int>("ExtensionApproved", (requestId, sessionId, minutes) =>
            {
                OnExtensionApproved?.Invoke(requestId, sessionId, minutes);
            });

            // Handle extension rejected
            _connection.On<string, int, string>("ExtensionRejected", (requestId, sessionId, reason) =>
            {
                OnExtensionRejected?.Invoke(requestId, sessionId, reason);
            });

            // Handle reconnection
            _connection.Reconnected += async (connectionId) =>
            {
                OnConnectionStatusChanged?.Invoke("Reconnected");
                await RegisterAsCustomerAsync(userId);
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
                await RegisterAsCustomerAsync(userId);
                OnConnectionStatusChanged?.Invoke("Connected");
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged?.Invoke($"Failed: {ex.Message}");
            }
        }

        // Register this client as a customer on the hub
        private async Task RegisterAsCustomerAsync(int userId)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync(
                    "RegisterAsCustomer", userId);
            }
        }

        // Notify server that session has started
        public async Task NotifySessionStartedAsync(
            int userId, string fullName,
            int sessionId, int allocatedMinutes)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync(
                    "NotifySessionStarted",
                    userId, fullName, sessionId, allocatedMinutes
                );
            }
        }

        // Send timer update every 30 seconds to admin
        public async Task SendTimerUpdateAsync(
            int userId, int sessionId,
            string remainingTime, decimal currentCost)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync(
                    "SendTimerUpdate",
                    userId, sessionId, remainingTime, currentCost
                );
            }
        }

        // Notify server that session has ended
        public async Task NotifySessionEndedAsync(
            int userId, int sessionId,
            int totalMinutes, decimal totalAmount)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync(
                    "NotifySessionEnded",
                    userId, sessionId, totalMinutes, totalAmount
                );
            }
        }

        // Request session extension from admin
        public async Task RequestExtensionAsync(string requestId, int sessionId, int userId, string customerName, int minutes, decimal amount)
        {
            if (_connection != null && IsConnected)
            {
                await _connection.InvokeAsync(
                    "RequestExtension",
                    requestId, sessionId, userId, customerName, minutes, amount
                );
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
