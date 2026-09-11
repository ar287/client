using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class AuditExportWindow : Window
    {
        private readonly ApiService _apiService;
        private List<ForensicRecordDto> _timelineRecords = new List<ForensicRecordDto>();

        public AuditExportWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            Loaded += AuditExportWindow_Loaded;
        }

        private async void AuditExportWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadTimelineAsync();
        }

        private AuditExportFilterDto BuildFilterFromUI()
        {
            var catItem = CategoryCombo.SelectedItem as ComboBoxItem;
            string category = catItem?.Tag?.ToString() ?? "All";

            var formatItem = FormatCombo.SelectedItem as ComboBoxItem;
            string format = formatItem?.Tag?.ToString() ?? "CSV";

            string clientId = ClientIdBox.Text.Trim();

            return new AuditExportFilterDto
            {
                SourceCategory = category,
                ClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId,
                Format = format,
                StartDateUtc = DateTime.UtcNow.AddDays(-30),
                EndDateUtc = DateTime.UtcNow.AddDays(1)
            };
        }

        private async Task LoadTimelineAsync()
        {
            StatusText.Text = "Compiling forensic audit records...";
            var filter = BuildFilterFromUI();

            _timelineRecords = await _apiService.GetForensicTimelineAsync(filter);

            TimelineDataGrid.ItemsSource = null;
            TimelineDataGrid.ItemsSource = _timelineRecords;

            StatusText.Text = $"Compiled { _timelineRecords.Count } forensic log record(s) at {DateTime.Now:HH:mm:ss}.";
        }

        private async void PreviewTimeline_Click(object sender, RoutedEventArgs e)
        {
            await LoadTimelineAsync();
        }

        private async void ExportFile_Click(object sender, RoutedEventArgs e)
        {
            var filter = BuildFilterFromUI();
            StatusText.Text = $"Generating {filter.Format} export report...";

            var exportResult = await _apiService.GenerateAuditExportAsync(filter);
            if (exportResult == null || string.IsNullOrWhiteSpace(exportResult.ContentString))
            {
                MessageBox.Show("Failed to generate audit export file.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Export generation failed.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = exportResult.SuggestedFileName,
                Filter = filter.Format.Equals("JSON", StringComparison.OrdinalIgnoreCase)
                    ? "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
                    : "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await File.WriteAllTextAsync(dialog.FileName, exportResult.ContentString);
                    MessageBox.Show($"Forensic audit report saved successfully to:\n{dialog.FileName}\n\nTotal Records: {exportResult.RecordCount}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusText.Text = $"Report exported to {Path.GetFileName(dialog.FileName)} ({exportResult.RecordCount} records).";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText.Text = "File save failed.";
                }
            }
            else
            {
                StatusText.Text = "Export cancelled by user.";
            }
        }
    }
}
