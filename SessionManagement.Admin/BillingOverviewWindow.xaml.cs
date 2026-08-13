using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public class AdminBillingRecordView
    {
        private readonly BillingRecord _record;

        public AdminBillingRecordView(BillingRecord record)
        {
            _record = record;
        }

        public string FullName            => _record.FullName;
        public int    SessionId           => _record.SessionId;
        public string GeneratedAt         => _record.GeneratedAt;
        public string TotalMinutesDisplay => $"{_record.TotalMinutes} min";
        public string RateDisplay         => $"Rs. {_record.RatePerMinute:F2}";
        public string TotalAmountDisplay  => $"Rs. {(_record.TotalAmount):F2}";
        public string SessionStatus       => _record.SessionStatus;
        public string PaidDisplay         => _record.IsPaid ? "✓ Paid" : "✗ Unpaid";
        
        // Helper for raw data access
        public BillingRecord RawRecord => _record;
    }

    public partial class BillingOverviewWindow : Window
    {
        private readonly ApiService _apiService;
        private List<BillingRecord> _allRecords = new List<BillingRecord>();

        public BillingOverviewWindow()
        {
            try
            {
                InitializeComponent();
                _apiService = new ApiService();
                Loaded += async (s, e) => await LoadBillingAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"BillingOverviewWindow Init Error: {ex.Message}\n\n{ex.StackTrace}");
            }
        }

        private async Task LoadBillingAsync()
        {
            BillingListView.Visibility = Visibility.Collapsed;

            BillingResponse? response = await _apiService.GetAllBillingAsync();

            if (response == null || !response.Success)
            {
                _allRecords = new List<BillingRecord>();
                ApplyLocalFilters();
                return;
            }

            _allRecords = response.Records ?? new List<BillingRecord>();
            ApplyLocalFilters();
            
            BillingListView.Visibility = Visibility.Visible;
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyLocalFilters();
        }

        private void ApplyLocalFilters()
        {
            if (_allRecords == null) return;
            
            // Check ALL UI controls accessed below to prevent crashes during InitializeComponent
            if (SearchBox == null || StartDatePicker == null || EndDatePicker == null || 
                StatusFilter == null || BillingListView == null || RecordCountText == null) 
            {
                return;
            }

            string search = SearchBox.Text.Trim().ToLower();
            
            DateTime? startDate = StartDatePicker.SelectedDate;
            DateTime? endDate   = EndDatePicker.SelectedDate;

            // 0: All, 1: Paid, 2: Unpaid
            int statusIndex = StatusFilter.SelectedIndex;

            var filtered = _allRecords.AsEnumerable();

            // Search Filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(r => 
                    (r.FullName != null && r.FullName.ToLower().Contains(search)) ||
                    r.SessionId.ToString().Contains(search));
            }

            // Date Filter
            if (startDate != null || endDate != null)
            {
                filtered = filtered.Where(r =>
                {
                    if (DateTime.TryParse(r.GeneratedAt, out DateTime genDate))
                    {
                        if (startDate != null && genDate.Date < startDate.Value.Date) return false;
                        if (endDate != null && genDate.Date > endDate.Value.Date) return false;
                        return true;
                    }
                    return true;
                });
            }

            // Status Filter
            if (statusIndex == 1) // Paid Only
                filtered = filtered.Where(r => r.IsPaid);
            else if (statusIndex == 2) // Unpaid Only
                filtered = filtered.Where(r => !r.IsPaid);

            var result = filtered
                .Select(r => new AdminBillingRecordView(r))
                .ToList();

            BillingListView.ItemsSource = result;
            RecordCountText.Text = $"{result.Count} record(s) found";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadBillingAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
