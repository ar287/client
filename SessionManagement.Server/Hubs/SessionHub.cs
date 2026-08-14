using Microsoft.AspNetCore.SignalR;

namespace SessionManagement.Server.Hubs
{
    public class SessionHub : Hub
    {
        // Called when a client connects
        public override async Task OnConnectedAsync()
        {
            string connectionId = Context.ConnectionId;
            Console.WriteLine($"[SignalR] Client connected: {connectionId}");
            await base.OnConnectedAsync();
        }

        // Called when a client disconnects
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string connectionId = Context.ConnectionId;
            Console.WriteLine($"[SignalR] Client disconnected: {connectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        // Client registers itself as Admin
        public async Task JoinAdminGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }

        // Server pushes security alert to all admins
        public async Task BroadcastSecurityAlert(
            string alertType,
            string message,
            string severity)
        {
            await Clients.Group("Admins").SendAsync(
                "SecurityAlert",
                alertType, message, severity
            );
        }

        // Client registers itself as Customer with userId
        public async Task RegisterAsCustomer(int userId)
        {
            string groupName = $"Customer_{userId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Console.WriteLine($"[SignalR] Customer {userId} registered.");
        }

        // Customer notifies server that session started
        public async Task NotifySessionStarted(
            int userId, string fullName,
            int sessionId, int allocatedMinutes)
        {
            Console.WriteLine(
                $"[SignalR] Session started — User: {fullName}, " +
                $"Session: {sessionId}"
            );

            await Clients.Group("Admins").SendAsync(
                "SessionStarted",
                userId, fullName, sessionId, allocatedMinutes
            );
        }

        // Customer sends periodic timer updates to admins
        public async Task SendTimerUpdate(
            int userId, int sessionId,
            string remainingTime, decimal currentCost)
        {
            await Clients.Group("Admins").SendAsync(
                "TimerUpdated",
                userId, sessionId, remainingTime, currentCost
            );
        }

        // Customer notifies server that session ended
        public async Task NotifySessionEnded(
            int userId, int sessionId,
            int totalMinutes, decimal totalAmount)
        {
            Console.WriteLine(
                $"[SignalR] Session ended — User: {userId}, " +
                $"Duration: {totalMinutes} min"
            );

            await Clients.Group("Admins").SendAsync(
                "SessionEnded",
                userId, sessionId, totalMinutes, totalAmount
            );
        }

        // Admin sends terminate command to a specific customer
        public async Task TerminateSession(int userId, string reason)
        {
            string groupName = $"Customer_{userId}";
            Console.WriteLine(
                $"[SignalR] Admin terminating session for User {userId}"
            );

            await Clients.Group(groupName).SendAsync(
                "SessionTerminated",
                reason
            );
        }

        // Admin sends a warning message to a specific customer
        public async Task SendWarningToCustomer(int userId, string message)
        {
            string groupName = $"Customer_{userId}";
            await Clients.Group(groupName).SendAsync(
                "WarningReceived",
                message
            );
        }

        // Customer requests session extension (Payment pending)
        public async Task RequestExtension(string requestId, int sessionId, int userId, string customerName, int minutes, decimal amount)
        {
            Console.WriteLine($"[SignalR] Extension requested — User: {customerName} ({userId}), Session: {sessionId}, +{minutes}m (Rs. {amount})");
            await Clients.Group("Admins").SendAsync(
                "ExtensionRequested",
                requestId, sessionId, userId, customerName, minutes, amount
            );
        }

        // Admin approves session extension request
        public async Task ApproveExtension(string requestId, int sessionId, int userId, int minutes)
        {
            Console.WriteLine($"[SignalR] Extension approved by Admin — User: {userId}, Session: {sessionId}, +{minutes}m");
            string groupName = $"Customer_{userId}";
            await Clients.Group(groupName).SendAsync(
                "ExtensionApproved",
                requestId, sessionId, minutes
            );
        }

        // Admin rejects session extension request
        public async Task RejectExtension(string requestId, int sessionId, int userId, string reason)
        {
            Console.WriteLine($"[SignalR] Extension rejected by Admin — User: {userId}, Session: {sessionId}");
            string groupName = $"Customer_{userId}";
            await Clients.Group(groupName).SendAsync(
                "ExtensionRejected",
                requestId, sessionId, reason
            );
        }
    }
}
