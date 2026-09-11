using System;
using System.Threading.Tasks;
using System.Windows;
using SessionManagement.Admin.Services;

namespace SessionManagement.Admin
{
    public partial class AIInsightsDashboardWindow : Window
    {
        private readonly ApiService _apiService;

        public AIInsightsDashboardWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        private async void GenerateSummaryBtn_Click(object sender, RoutedEventArgs e)
        {
            GenerateSummaryBtn.IsEnabled = false;
            SummaryText.Text = "Generating executive shift summary via Ollama AI...";

            try
            {
                var summary = await _apiService.GenerateAILogSummaryAsync(50);
                if (summary != null)
                {
                    SummaryText.Text = summary.Summary;
                    OperationalRiskText.Text = summary.OperationalRisk;
                    KeyEventsList.ItemsSource = summary.KeyEvents;
                }
                else
                {
                    SummaryText.Text = "Could not generate summary.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AI summary failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GenerateSummaryBtn.IsEnabled = true;
            }
        }

        private async void RunAiAnalysisBtn_Click(object sender, RoutedEventArgs e)
        {
            string pcId = AiTargetPcInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(pcId)) return;

            RunAiAnalysisBtn.IsEnabled = false;
            AiScoreText.Text = "Analyzing...";

            try
            {
                var assessment = await _apiService.EvaluateClientRiskWithAiAsync(pcId);
                if (assessment != null)
                {
                    AiScoreText.Text = $"{assessment.RiskScore} / 100 ({assessment.RiskLevel})";
                    AiModelText.Text = assessment.EvaluatedBy;
                    AiConfidenceText.Text = $"{assessment.ConfidenceScore * 100:F0}%";
                    AiRecommendedActionText.Text = assessment.RecommendedAction;
                    AiExplanationsList.ItemsSource = assessment.Reasons;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AI risk analysis failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RunAiAnalysisBtn.IsEnabled = true;
            }
        }
    }
}
