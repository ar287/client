using System;
using System.Collections.Generic;
using System.Media;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class AlertCenterWindow : Window
    {
        private readonly HttpClient      _httpClient;
        private List<SecurityAlert>      _allAlerts = new List<SecurityAlert>();
        private DispatcherTimer          _clockTimer;
        private const string BaseUrl     = "http://localhost:5102";

        public AlertCenterWindow()
        {
            try
            {
                InitializeComponent();

                _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };

                _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _clockTimer.Tick += (s, e) => {
                    if (ClockText != null) ClockText.Text = DateTime.Now.ToString("F");
                };
                _clockTimer.Start();

                this.Loaded += async (s, e) => await LoadAlertsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AlertCenterWindow Init Error: {ex.Message}\n\n{ex.StackTrace}");
            }
        }

        private async Task LoadAlertsAsync()
        {
            try
            {
                StatusText.Text = "Fetching alerts...";
                EmptyBorder.Visibility = Visibility.Visible;
                EmptyText.Text = "Loading alerts...";
                AlertsListView.Visibility = Visibility.Collapsed;

                var response = await _httpClient.GetFromJsonAsync<SecurityAlertsResponse>("api/security/alerts");
                if (response != null && response.Success)
                {
                    _allAlerts = response.Alerts ?? new List<SecurityAlert>();
                    ApplyLocalFilters();
                }
                else
                {
                    EmptyText.Text = "Failed to load alerts.";
                }
            }
            catch (Exception ex)
            {
                EmptyText.Text = "Error: " + ex.Message;
            }
            finally
            {
                StatusText.Text = "Ready";
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyLocalFilters();
        }

        private void ApplyLocalFilters()
        {
            if (_allAlerts == null) return;
            
            // Check ALL UI controls accessed below to prevent crashes during InitializeComponent
            if (SearchBox == null || SeverityFilter == null || TypeFilter == null || ReadFilter == null || 
                StartDatePicker == null || EndDatePicker == null || AlertCountText == null || 
                AlertsListView == null || EmptyBorder == null || TotalCount == null || 
                HighCount == null || MediumCount == null || LowCount == null || UnreadCount == null) 
            {
                return;
            }

            string search = SearchBox.Text.Trim().ToLower();
            
            string severity = null;
            if (SeverityFilter.SelectedIndex == 1) severity = "High";
            if (SeverityFilter.SelectedIndex == 2) severity = "Medium";
            if (SeverityFilter.SelectedIndex == 3) severity = "Low";

            string type = null;
            if (TypeFilter.SelectedIndex == 1) type = "FailedLogin";
            if (TypeFilter.SelectedIndex == 2) type = "DuplicateSession";
            if (TypeFilter.SelectedIndex == 3) type = "RateLimit";
            if (TypeFilter.SelectedIndex == 4) type = "SessionTerminated";

            bool unreadOnly = ReadFilter != null && ReadFilter.SelectedIndex == 1;

            DateTime? startDate = StartDatePicker?.SelectedDate;
            DateTime? endDate   = EndDatePicker?.SelectedDate;

            var filtered = _allAlerts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
                filtered = filtered.Where(a =>
                    (a.AlertType != null && a.AlertType.ToLower().Contains(search))   ||
                    (a.Description != null && a.Description.ToLower().Contains(search)) ||
                    (a.Username != null && a.Username.ToLower().Contains(search)));

            if (severity != null)
                filtered = filtered.Where(a => a.Severity == severity);

            if (type != null)
                filtered = filtered.Where(a => a.AlertType == type);

            if (unreadOnly)
                filtered = filtered.Where(a => !a.IsRead);

            // Date filtering
            if (startDate != null || endDate != null)
            {
                filtered = filtered.Where(a =>
                {
                    if (DateTime.TryParse(a.CreatedAt, out DateTime alertDate))
                    {
                        if (startDate != null && alertDate.Date < startDate.Value.Date) return false;
                        if (endDate != null && alertDate.Date > endDate.Value.Date) return false;
                        return true;
                    }
                    return true;
                });
            }

            var result = filtered.ToList();

            AlertCountText.Text = $"{result.Count} alert(s)";
            AlertsListView.ItemsSource = result;

            if (result.Count > 0)
            {
                EmptyBorder.Visibility = Visibility.Collapsed;
                AlertsListView.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyBorder.Visibility = Visibility.Visible;
                EmptyText.Text = "No matching alerts found.";
                AlertsListView.Visibility = Visibility.Collapsed;
            }

            UpdateStats();
        }

        private void UpdateStats()
        {
            TotalCount.Text  = _allAlerts.Count.ToString();
            HighCount.Text   = _allAlerts.Count(a => a.Severity == "High").ToString();
            MediumCount.Text = _allAlerts.Count(a => a.Severity == "Medium").ToString();
            LowCount.Text    = _allAlerts.Count(a => a.Severity == "Low").ToString();
            UnreadCount.Text = _allAlerts.Count(a => !a.IsRead).ToString();
        }

        private void AlertsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = AlertsListView.SelectedItem as SecurityAlert;
            if (selected == null)
            {
                NoSelectionPanel.Visibility = Visibility.Visible;
                DetailPanel.Visibility = Visibility.Collapsed;
                return;
            }

            NoSelectionPanel.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Visible;

            DetailSeverityIcon.Text = selected.SeverityIcon;
            DetailSeverityText.Text = selected.Severity.ToUpper();
            DetailTypeText.Text     = selected.AlertType;
            DetailTimeText.Text     = selected.CreatedAt;
            DetailUserText.Text     = selected.Username;
            DetailDescText.Text     = selected.Description;

            // Severity styling
            if (selected.Severity == "High")
            {
                SeverityBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDEDEC"));
                DetailSeverityText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0392B"));
            }
            else if (selected.Severity == "Medium")
            {
                SeverityBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF9F0"));
                DetailSeverityText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D68910"));
            }
            else
            {
                SeverityBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAFAF1"));
                DetailSeverityText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E8449"));
            }

            MarkReadButton.Visibility = selected.IsRead ? Visibility.Collapsed : Visibility.Visible;

            // Reset AI Card State for newly selected alert
            if (selected.AIThreatScore.HasValue)
            {
                AIThreatBadgeText.Text = selected.AIThreatBadge;
                AIExplanationText.Text = selected.AIExplanation ?? "";
                AIActionText.Text = "💡 Recommendation: " + (selected.AIRecommendedAction ?? "");
            }
            else
            {
                AIThreatBadgeText.Text = "⚪ Click 'Run AI Triage' for local AI analysis";
                AIExplanationText.Text = "";
                AIActionText.Text = "";
            }
        }

        private async void AIAnalyze_Click(object sender, RoutedEventArgs e)
        {
            var selected = AlertsListView.SelectedItem as SecurityAlert;
            if (selected == null) return;

            try
            {
                AIAnalyzeButton.IsEnabled = false;
                AIAnalyzeButton.Content = "⏳ Analyzing...";
                AIThreatBadgeText.Text = "🤖 Ollama model analyzing alert & context logs...";

                var apiService = new Services.ApiService();
                var analyzed = await apiService.AnalyzeSecurityAlertWithAIAsync(selected.AlertId);

                if (analyzed != null)
                {
                    selected.AIThreatScore = analyzed.AIThreatScore;
                    selected.AIClassification = analyzed.AIClassification;
                    selected.AIExplanation = analyzed.AIExplanation;
                    selected.AIRecommendedAction = analyzed.AIRecommendedAction;

                    AIThreatBadgeText.Text = selected.AIThreatBadge + $" ({selected.AIThreatScore}/100)";
                    AIExplanationText.Text = "🔍 " + (selected.AIExplanation ?? "");
                    AIActionText.Text = "💡 Recommendation: " + (selected.AIRecommendedAction ?? "");
                }
                else
                {
                    AIThreatBadgeText.Text = "⚠️ AI Analysis unavailable (Ollama engine offline).";
                }
            }
            catch (Exception ex)
            {
                AIThreatBadgeText.Text = "Error: " + ex.Message;
            }
            finally
            {
                AIAnalyzeButton.IsEnabled = true;
                AIAnalyzeButton.Content = "⚡ Run AI Triage";
            }
        }

        private async void MarkSingleRead_Click(object sender, RoutedEventArgs e)
        {
            var selected = AlertsListView.SelectedItem as SecurityAlert;
            if (selected == null) return;

            try
            {
                var response = await _httpClient.PutAsync($"api/security/markread/{selected.AlertId}", null);
                if (response.IsSuccessStatusCode)
                {
                    selected.IsRead = true;
                    ApplyLocalFilters();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error marking read: " + ex.Message);
            }
        }

        private async void MarkAllRead_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var response = await _httpClient.PutAsync("api/security/markread", null);
                if (response.IsSuccessStatusCode)
                {
                    foreach (var a in _allAlerts) a.IsRead = true;
                    ApplyLocalFilters();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error marking all read: " + ex.Message);
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAlertsAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        public void AddLiveAlert(SecurityAlert alert)
        {
            _allAlerts.Insert(0, alert);
            
            Dispatcher.Invoke(() => {
                ApplyLocalFilters();
                if (alert.Severity == "High")
                {
                    SystemSounds.Exclamation.Play();
                }
            });
        }
    }
}
