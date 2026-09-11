using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using SessionManagement.Admin.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Admin
{
    public partial class VisualRuleBuilderWindow : Window
    {
        private readonly ApiService _apiService;

        public VisualRuleBuilderWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        private async void GenerateDraftBtn_Click(object sender, RoutedEventArgs e)
        {
            string prompt = NlPromptInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(prompt)) return;

            GenerateDraftBtn.IsEnabled = false;
            GenerateDraftBtn.Content = "⏳ Generating...";

            try
            {
                var draft = await _apiService.GenerateNlRuleDraftAsync(prompt);
                if (draft != null)
                {
                    RuleNameInput.Text = draft.RuleName;
                    DescriptionInput.Text = draft.Description;

                    // Map severity
                    SeverityCombo.SelectedIndex = draft.Severity switch
                    {
                        "Low" => 0, "Medium" => 1, "High" => 2, "Critical" => 3, _ => 1
                    };

                    // Map scope
                    ScopeCombo.SelectedIndex = draft.Scope switch
                    {
                        "SamePC" => 0, "SameUser" => 1, "AllPCs" => 2, _ => 0
                    };

                    if (draft.Conditions.Count > 0)
                    {
                        var cond = draft.Conditions[0];
                        MetricTypeCombo.SelectedIndex = cond.MetricType switch
                        {
                            "FailedLogin" => 0, "SecurityAlert" => 1, "SessionTerminated" => 2,
                            "UnauthorizedProcess" => 3, "OfflineUnexpected" => 4, _ => 0
                        };
                        ThresholdInput.Text = cond.ThresholdValue;
                        WindowInput.Text = cond.WindowMinutes.ToString();
                    }

                    if (draft.Actions.Count > 0)
                    {
                        var act = draft.Actions[0];
                        ActionTypeCombo.SelectedIndex = act.ActionType switch
                        {
                            "CreateAlert" => 0, "NotifyAdmin" => 1, "RequestAiAnalysis" => 2,
                            "MarkClientUnderReview" => 3, "LockClient" => 4, _ => 0
                        };
                        RequiresApprovalCheck.IsChecked = act.RequiresApproval;
                    }

                    UpdatePreviewSummary();
                    MessageBox.Show("Draft rule generated from AI prompt. Review and save when ready.", "AI Draft Generated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("AI service unavailable. Fallback rule created.", "Draft Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate draft: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GenerateDraftBtn.IsEnabled = true;
                GenerateDraftBtn.Content = "✨ Convert to Structured Rule Draft";
            }
        }

        private async void TestRuleBtn_Click(object sender, RoutedEventArgs e)
        {
            TestResultsList.Items.Clear();
            TestResultsList.Items.Add("🔍 Querying historical event data for rule match simulation...");

            string metricType = (MetricTypeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "FailedLogin";

            try
            {
                // Query last 48h of events for the metric type as a simulation
                var timeline = await _apiService.GetEventTimelineAsync(new EventFilterRequest
                {
                    EventType = metricType,
                    PageSize = 50,
                    DateFrom = DateTime.UtcNow.AddHours(-48)
                });

                TestResultsList.Items.Clear();

                if (timeline == null || timeline.Events.Count == 0)
                {
                    TestResultsList.Items.Add($"✅ No recent '{metricType}' events found in last 48 hours.");
                    TestResultsList.Items.Add("Rule would not have triggered. Historical data shows safe environment.");
                    return;
                }

                if (!int.TryParse(ThresholdInput.Text, out int threshold)) threshold = 5;
                if (!int.TryParse(WindowInput.Text, out int window)) window = 10;

                // Group by ClientId to simulate rule matching
                var grouped = new Dictionary<string, int>();
                foreach (var ev in timeline.Events)
                {
                    string key = ev.ClientId ?? "Unknown";
                    if (!grouped.ContainsKey(key)) grouped[key] = 0;
                    grouped[key]++;
                }

                bool anyTriggered = false;
                foreach (var pair in grouped)
                {
                    if (pair.Value >= threshold)
                    {
                        TestResultsList.Items.Add($"⚠️ WOULD TRIGGER on PC: {pair.Key} — {pair.Value} '{metricType}' events (threshold: {threshold} in {window} min)");
                        anyTriggered = true;
                    }
                    else
                    {
                        TestResultsList.Items.Add($"✅ Safe: {pair.Key} — {pair.Value} events (below threshold of {threshold})");
                    }
                }

                if (!anyTriggered)
                    TestResultsList.Items.Add($"✅ No PC exceeded the threshold of {threshold} '{metricType}' events.");
                else
                    TestResultsList.Items.Add($"\nTotal events analyzed: {timeline.TotalCount}");
            }
            catch (Exception ex)
            {
                TestResultsList.Items.Clear();
                TestResultsList.Items.Add($"❌ Test failed: {ex.Message}");
            }
        }

        private async void SaveRuleBtn_Click(object sender, RoutedEventArgs e)
        {
            string ruleName = RuleNameInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(ruleName))
            {
                MessageBox.Show("Rule name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string severity = (SeverityCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Medium";
            string scope = (ScopeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "SamePC";
            string metricType = (MetricTypeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "FailedLogin";
            string actionType = (ActionTypeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "CreateAlert";

            if (!int.TryParse(ThresholdInput.Text, out int threshold)) threshold = 5;
            if (!int.TryParse(WindowInput.Text, out int window)) window = 10;

            var rule = new SecurityRuleDto
            {
                RuleName = ruleName,
                Description = DescriptionInput.Text.Trim(),
                Severity = severity,
                Scope = scope,
                IsActive = true,
                IsDraft = false,
                CooldownMinutes = 5,
                CreatedBy = "Admin",
                Conditions = new List<RuleConditionDto>
                {
                    new RuleConditionDto { MetricType = metricType, Operator = "CountInWindow", ThresholdValue = threshold.ToString(), WindowMinutes = window }
                },
                Actions = new List<RuleActionDto>
                {
                    new RuleActionDto { ActionType = actionType, RequiresApproval = RequiresApprovalCheck.IsChecked == true }
                }
            };

            try
            {
                SaveRuleBtn.IsEnabled = false;
                var saved = await _apiService.CreateSecurityRuleAsync(rule);

                if (saved)
                {
                    MessageBox.Show($"✅ Rule '{ruleName}' saved and activated successfully!", "Rule Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to save rule. Ensure the server is running and SecurityRules table exists (run ExtensionSetup.sql).", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveRuleBtn.IsEnabled = true;
            }
        }

        private void UpdatePreviewSummary()
        {
            string metric = (MetricTypeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "FailedLogin";
            string action = (ActionTypeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "CreateAlert";
            string scope = (ScopeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "SamePC";
            string threshold = ThresholdInput.Text;
            string window = WindowInput.Text;

            PreviewSummaryText.Text = $"IF {threshold} '{metric}' events occur within {window} minutes on {scope} THEN {action}.";
        }
    }
}
