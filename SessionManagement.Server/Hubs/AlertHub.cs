using Microsoft.AspNetCore.SignalR;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Hubs
{
    /// <summary>
    /// Dedicated hub for real-time security alert broadcast from the Rule Engine.
    /// All Admin WPF clients join the "Admins" group and receive rule-fired alerts instantly.
    /// </summary>
    public class AlertHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"[AlertHub] Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[AlertHub] Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>Admin WPF calls this to join the alert broadcast group.</summary>
        public async Task JoinAlertGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "AlertAdmins");
            Console.WriteLine($"[AlertHub] Admin joined alert group: {Context.ConnectionId}");
        }

        /// <summary>
        /// Server-side (RuleEngineService) calls IHubContext to broadcast — this hub
        /// method is here for documentation; server uses IHubContext directly.
        /// </summary>
        public async Task BroadcastRuleAlert(SecurityAlertNotification alert)
        {
            await Clients.Group("AlertAdmins").SendAsync("RuleAlert", alert);
        }
    }
}
