using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SessionManagement.Admin.Services;

namespace SessionManagement.Admin
{
    public partial class SecurityDashboardWindow : Window
    {
        private readonly ApiService _apiService;

        public SecurityDashboardWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
            Loaded += async (s, e) => await LoadRulesAsync();
        }

        private async Task LoadRulesAsync()
        {
            try
            {
                var rules = await _apiService.GetSecurityRulesAsync();
                RulesGrid.ItemsSource = rules;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load rules: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void EvaluateRiskBtn_Click(object sender, RoutedEventArgs e)
        {
            string pcId = TargetPcInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(pcId)) return;

            try
            {
                var assessment = await _apiService.EvaluateClientRiskAsync(pcId);
                if (assessment != null)
                {
                    RiskTargetText.Text = assessment.ClientId;
                    RiskScoreText.Text = $"{assessment.RiskScore} / 100";
                    RiskLevelText.Text = assessment.RiskLevel.ToUpper();
                    EvaluatedByText.Text = assessment.EvaluatedBy;
                    RecommendedActionText.Text = assessment.RecommendedAction;

                    // Color code score badge
                    Brush color = assessment.RiskLevel switch
                    {
                        "Critical" => Brushes.Red,
                        "High" => Brushes.OrangeRed,
                        "Medium" => Brushes.DarkOrange,
                        _ => Brushes.Green
                    };
                    RiskScoreText.Foreground = color;
                    RiskBadge.Background = color;

                    ReasonsListBox.ItemsSource = assessment.Reasons.Count > 0 ? assessment.Reasons : new[] { "No active rule violations." };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Risk evaluation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
