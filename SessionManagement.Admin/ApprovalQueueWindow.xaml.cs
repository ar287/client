using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class ApprovalQueueWindow : Window
    {
        private readonly ApiService _apiService;
        private List<PendingApprovalDto> _approvals = new List<PendingApprovalDto>();
        private PendingApprovalDto? _selectedItem;

        public ApprovalQueueWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            Loaded += ApprovalQueueWindow_Loaded;
        }

        private async void ApprovalQueueWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            StatusText.Text = "Loading pending approvals...";
            _approvals = await _apiService.GetPendingApprovalsAsync();

            ApprovalsDataGrid.ItemsSource = null;
            ApprovalsDataGrid.ItemsSource = _approvals;

            int total = _approvals.Count;
            int pending = _approvals.Count(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            int approved = _approvals.Count(x => string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase));
            int rejected = _approvals.Count(x => string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase));

            TotalRequestsText.Text = total.ToString();
            PendingCountText.Text = pending.ToString();
            ApprovedCountText.Text = approved.ToString();
            RejectedCountText.Text = rejected.ToString();

            StatusText.Text = $"Updated at {DateTime.Now:HH:mm:ss} — {pending} pending request(s).";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void ApprovalsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedItem = ApprovalsDataGrid.SelectedItem as PendingApprovalDto;
            if (_selectedItem == null)
            {
                DetailTitleText.Text = "Select a request from the table";
                DetailClientText.Text = "Client: -";
                DetailActionText.Text = "Action: -";
                DetailReasonText.Text = "Reason: -";
                DetailStatusText.Text = "Status: -";

                ApproveButton.IsEnabled = false;
                RejectButton.IsEnabled = false;
                return;
            }

            DetailTitleText.Text = $"[{_selectedItem.Id}] {_selectedItem.RuleName}";
            DetailClientText.Text = $"Client: {_selectedItem.ClientId}";
            DetailActionText.Text = $"Action: {_selectedItem.ActionType} ({_selectedItem.ActionTarget ?? "Default"})";
            DetailReasonText.Text = $"Reason: {_selectedItem.Reason ?? "Rule threshold triggered."}";
            DetailStatusText.Text = $"Status: {_selectedItem.Status}";

            bool isPending = string.Equals(_selectedItem.Status, "Pending", StringComparison.OrdinalIgnoreCase);
            ApproveButton.IsEnabled = isPending;
            RejectButton.IsEnabled = isPending;
        }

        private async void Approve_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;

            string reviewer = ReviewerNameBox.Text.Trim();
            string notes = ReviewNotesBox.Text.Trim();

            StatusText.Text = $"Authorizing item #{_selectedItem.Id}...";
            bool success = await _apiService.ApprovePendingActionAsync(_selectedItem.Id, reviewer, notes);

            if (success)
            {
                MessageBox.Show($"Action #{_selectedItem.Id} authorized successfully.", "Authorized", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            else
            {
                MessageBox.Show("Failed to authorize request.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Authorization failed.";
            }
        }

        private async void Reject_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;

            string reviewer = ReviewerNameBox.Text.Trim();
            string notes = ReviewNotesBox.Text.Trim();

            StatusText.Text = $"Rejecting item #{_selectedItem.Id}...";
            bool success = await _apiService.RejectPendingActionAsync(_selectedItem.Id, reviewer, notes);

            if (success)
            {
                MessageBox.Show($"Action #{_selectedItem.Id} rejected.", "Rejected", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            else
            {
                MessageBox.Show("Failed to reject request.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Rejection failed.";
            }
        }
    }
}
