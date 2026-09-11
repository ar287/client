using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class RemediationControlWindow : Window
    {
        private readonly ApiService _apiService;
        private List<RemediationLogDto> _remediationLogs = new List<RemediationLogDto>();

        public RemediationControlWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            Loaded += RemediationControlWindow_Loaded;
        }

        private async void RemediationControlWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadLogsAsync();
        }

        private async Task LoadLogsAsync()
        {
            StatusText.Text = "Loading remediation execution logs...";
            _remediationLogs = await _apiService.GetRemediationLogsAsync();

            RemediationDataGrid.ItemsSource = null;
            RemediationDataGrid.ItemsSource = _remediationLogs;

            StatusText.Text = $"Updated at {DateTime.Now:HH:mm:ss} — {_remediationLogs.Count} remediation record(s) loaded.";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadLogsAsync();
        }

        private async void ExecuteRemediation_Click(object sender, RoutedEventArgs e)
        {
            string clientId = ClientIdBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                MessageBox.Show("Target Client PC ID is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedItem = ActionTypeCombo.SelectedItem as ComboBoxItem;
            string actionType = selectedItem?.Tag?.ToString() ?? "LockScreen";
            string targetProcess = TargetProcessBox.Text.Trim();
            string reason = ReasonBox.Text.Trim();
            int.TryParse(RiskScoreBox.Text.Trim(), out int riskScore);

            var request = new RemediationRequestDto
            {
                ClientId = clientId,
                ActionType = actionType,
                TargetProcessName = string.IsNullOrWhiteSpace(targetProcess) ? null : targetProcess,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Manual mitigation triggered by admin." : reason,
                InitiatedBy = "Admin",
                RiskScore = riskScore
            };

            StatusText.Text = $"Dispatching {actionType} signal to {clientId}...";
            var result = await _apiService.ExecuteRemediationAsync(request);

            if (result != null && result.IsSuccess)
            {
                MessageBox.Show($"Remediation signal dispatched successfully to {clientId}.\n\nMessage: {result.Message}", "Remediation Dispatched", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadLogsAsync();
            }
            else
            {
                MessageBox.Show($"Failed to dispatch remediation signal to {clientId}.", "Execution Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Remediation execution failed.";
            }
        }
    }
}
