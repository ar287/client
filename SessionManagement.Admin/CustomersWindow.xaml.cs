using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class CustomersWindow : Window
    {
        private readonly ApiService  _apiService;
        private CustomerDto?         _selectedCustomer;
        private List<CustomerDto>    _allCustomers = new();

        public CustomersWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            Loaded += async (s, e) => await LoadCustomersAsync();
        }

        // ── LOAD ─────────────────────────────────────────────────────

        private async Task LoadCustomersAsync(
            string? search = null,
            string? status = null)
        {
            try
            {
                SetStatus("Loading customers...");
                LoadingBorder.Visibility   = Visibility.Visible;
                CustomersListView.Visibility = Visibility.Collapsed;

                var response =
                    await _apiService.GetCustomersAsync(search, status);

                if (response == null || !response.Success)
                {
                    LoadingText.Text = response?.Message
                        ?? "Could not load customers.";
                    SetStatus("Error loading customers.");
                    return;
                }

                _allCustomers = response.Customers;

                // Update stat cards
                TotalCustomersText.Text  = response.Total.ToString();
                ActiveCustomersText.Text = response.Active.ToString();
                InactiveCustomersText.Text = response.Inactive.ToString();
                TotalRevenueText.Text    =
                    $"Rs. {response.Customers.Sum(c => c.TotalSpent):F0}";
                TableSubtitleText.Text   = $"{response.Total} record(s)";

                if (response.Customers.Count == 0)
                {
                    LoadingText.Text = "No customers found.";
                    SetStatus("No customers found.");
                    return;
                }

                CustomersListView.ItemsSource  = response.Customers;
                LoadingBorder.Visibility       = Visibility.Collapsed;
                CustomersListView.Visibility   = Visibility.Visible;
                SetStatus($"{response.Total} customer(s) loaded.");
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}");
                LoadingText.Text = "An unexpected error occurred.";
            }
        }

        // ── FILTERS ──────────────────────────────────────────────────

        private async void SearchButton_Click(
            object sender, RoutedEventArgs e)
        {
            await ApplyFiltersAsync();
        }

        private async void ResetFilter_Click(
            object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            StatusFilterBox.SelectedIndex = 0;
            await LoadCustomersAsync();
        }

        private void SearchBox_TextChanged(
            object sender, TextChangedEventArgs e)
        {
            if (SearchBox == null) return;

            // Live search after 3 chars
            if (SearchBox.Text.Length == 0 ||
                SearchBox.Text.Length >= 3)
            {
                _ = ApplyFiltersAsync();
            }
        }

        private async void StatusFilter_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            await ApplyFiltersAsync();
        }

        private async Task ApplyFiltersAsync()
        {
            try
            {
                if (SearchBox == null || StatusFilterBox == null) return;

                string? search = string.IsNullOrWhiteSpace(SearchBox.Text)
                    ? null
                    : SearchBox.Text.Trim();

                string? status = StatusFilterBox.SelectedIndex switch
                {
                    1 => "Active",
                    2 => "Inactive",
                    _ => null
                };

                await LoadCustomersAsync(search, status);
            }
            catch { /* Ignore background filter errors to prevent crash */ }
        }

        // ── SELECTION ────────────────────────────────────────────────

        private void CustomersListView_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            _selectedCustomer =
                CustomersListView.SelectedItem as CustomerDto;

            bool selected = _selectedCustomer != null;
            EditButton.IsEnabled         = selected;
            ToggleStatusButton.IsEnabled = selected;
            DeleteButton.IsEnabled       = selected;

            if (selected)
            {
                string status = _selectedCustomer!.IsActive
                    ? "Active" : "Inactive";
                SetStatus(
                    $"Selected: {_selectedCustomer.FullName} " +
                    $"(@{_selectedCustomer.Username}) — {status}");
            }
            else
            {
                SetStatus("Select a customer to perform actions.");
            }
        }

        // ── ADD ──────────────────────────────────────────────────────

        private void AddCustomer_Click(
            object sender, RoutedEventArgs e)
        {
            var dialog = new CustomerFormWindow(null);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _ = LoadCustomersAsync();
            }
        }

        // ── EDIT ─────────────────────────────────────────────────────

        private void EditCustomer_Click(
            object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null) return;

            var dialog = new CustomerFormWindow(_selectedCustomer);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                _ = LoadCustomersAsync();
            }
        }

        // ── TOGGLE STATUS ────────────────────────────────────────────

        private async void ToggleStatus_Click(
            object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null) return;

            string action = _selectedCustomer.IsActive
                ? "deactivate" : "activate";

            MessageBoxResult confirm = MessageBox.Show(
                $"Are you sure you want to {action}:\n\n" +
                $"👤  {_selectedCustomer.FullName}\n" +
                $"🔖  @{_selectedCustomer.Username}",
                "Confirm Status Change",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (confirm != MessageBoxResult.Yes) return;

            ToggleStatusButton.IsEnabled = false;
            SetStatus("Updating status...");

            var response = await _apiService
                .ToggleCustomerStatusAsync(_selectedCustomer.UserId);

            ToggleStatusButton.IsEnabled = true;

            if (response != null && response.Success)
            {
                ShowNotification(response.Message, true);
                await LoadCustomersAsync();
            }
            else
            {
                ShowNotification(
                    response?.Message ?? "Failed to update status.",
                    false);
            }
        }

        // ── DELETE ───────────────────────────────────────────────────

        private async void DeleteCustomer_Click(
            object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null) return;

            MessageBoxResult confirm = MessageBox.Show(
                $"⚠  Permanently delete this customer?\n\n" +
                $"👤  {_selectedCustomer.FullName}\n" +
                $"🔖  @{_selectedCustomer.Username}\n\n" +
                $"This will also delete all their sessions, " +
                $"billing records, and logs.\n\n" +
                $"This action CANNOT be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (confirm != MessageBoxResult.Yes) return;

            DeleteButton.IsEnabled = false;
            SetStatus("Deleting customer...");

            var response = await _apiService
                .DeleteCustomerAsync(_selectedCustomer.UserId);

            DeleteButton.IsEnabled = true;

            if (response != null && response.Success)
            {
                ShowNotification(response.Message, true);
                _selectedCustomer = null;
                EditButton.IsEnabled         = false;
                ToggleStatusButton.IsEnabled = false;
                DeleteButton.IsEnabled       = false;
                await LoadCustomersAsync();
            }
            else
            {
                ShowNotification(
                    response?.Message ?? "Delete failed.",
                    false);
            }
        }

        // ── HELPERS ──────────────────────────────────────────────────

        private void SetStatus(string message)
        {
            StatusText.Text = message;
        }

        private void ShowNotification(string message, bool success)
        {
            MessageBox.Show(
                message,
                success ? "Success" : "Error",
                MessageBoxButton.OK,
                success
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Error
            );
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
