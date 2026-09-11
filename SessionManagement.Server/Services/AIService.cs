using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AIService> _logger;
        private readonly string _baseUrl;
        private readonly string _defaultModel;

        public AIService(IConfiguration configuration, ILogger<AIService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _baseUrl = configuration["OllamaConfig:BaseUrl"] ?? "http://localhost:11434";
            _defaultModel = configuration["OllamaConfig:Model"] ?? "llama3.2:3b";
        }

        public string DefaultModel => _defaultModel;
        public string BaseUrl => _baseUrl;

        /// <summary>
        /// Analyzes a security alert using Ollama AI to determine threat score and remediation.
        /// </summary>
        public async Task<SecurityAlert> AnalyzeSecurityAlertAsync(SecurityAlert alert, List<LogDto> recentLogs)
        {
            try
            {
                var logsContext = string.Join("\n", recentLogs.Take(10).Select(l => $"[{l.CreatedAt}] User: {l.Username}, Event: {l.EventType}, IP: {l.IPAddress}, Desc: {l.Description}"));

                var prompt = $@"You are a Cybersecurity Incident Response AI.
Analyze this security alert in a lab session management system:
Alert Type: {alert.AlertType}
Severity: {alert.Severity}
Username: {alert.Username}
Description: {alert.Description}

Recent Context Logs:
{logsContext}

Respond ONLY with a raw valid JSON object without markdown formatting:
{{
  ""threatScore"": 85,
  ""classification"": ""High Risk Credential Attack"",
  ""explanation"": ""Multiple suspicious actions detected in rapid succession."",
  ""recommendedAction"": ""Lock account immediately and inspect IP access.""
}}";

                var jsonResult = await CallOllamaGenerateAsync(prompt);
                if (!string.IsNullOrWhiteSpace(jsonResult))
                {
                    using var doc = JsonDocument.Parse(ExtractJson(jsonResult));
                    var root = doc.RootElement;
                    alert.AIThreatScore = root.TryGetProperty("threatScore", out var s) ? Math.Clamp(s.GetInt32(), 0, 100) : (alert.Severity == "High" ? 85 : 45);
                    alert.AIClassification = root.TryGetProperty("classification", out var c) ? c.GetString() : alert.AlertType;
                    alert.AIExplanation = root.TryGetProperty("explanation", out var e) ? e.GetString() : alert.Description;
                    alert.AIRecommendedAction = root.TryGetProperty("recommendedAction", out var a) ? a.GetString() : "Review user session permissions.";
                    return alert;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama AI analysis fallback triggered for alert ID {AlertId}", alert.AlertId);
            }

            // Fallback logic if Ollama is unavailable
            alert.AIThreatScore = alert.Severity switch { "High" => 80, "Medium" => 50, _ => 20 };
            alert.AIClassification = $"{alert.AlertType} (Rule Analysis)";
            alert.AIExplanation = $"Rule-based assessment: {alert.Description}";
            alert.AIRecommendedAction = alert.Severity == "High" ? "Lock account & audit IP." : "Monitor session activity.";
            return alert;
        }

        /// <summary>
        /// Summarizes system logs into an executive brief.
        /// </summary>
        public async Task<AILogSummaryDto> GenerateLogSummaryAsync(List<LogDto> logs)
        {
            var summaryResult = new AILogSummaryDto
            {
                Timestamp = DateTime.Now.ToString("g")
            };

            if (logs == null || !logs.Any())
            {
                summaryResult.Summary = "No recent log entries recorded.";
                summaryResult.KeyEvents = new List<string> { "System idle." };
                summaryResult.OperationalRisk = "Low";
                return summaryResult;
            }

            try
            {
                var sampleLogs = string.Join("\n", logs.Take(30).Select(l => $"[{l.CreatedAt}] {l.Username} | {l.EventType} | {l.Description}"));

                var prompt = $@"Summarize the following system operational logs into an executive shift brief.
Logs:
{sampleLogs}

Respond ONLY with a raw valid JSON object without markdown formatting:
{{
  ""summary"": ""Clear concise 2-sentence shift overview."",
  ""keyEvents"": [""Key event 1"", ""Key event 2""],
  ""operationalRisk"": ""Low/Medium/High""
}}";

                var jsonResult = await CallOllamaGenerateAsync(prompt);
                if (!string.IsNullOrWhiteSpace(jsonResult))
                {
                    using var doc = JsonDocument.Parse(ExtractJson(jsonResult));
                    var root = doc.RootElement;
                    summaryResult.Summary = root.GetProperty("summary").GetString() ?? "Shift summary generated.";
                    summaryResult.OperationalRisk = root.GetProperty("operationalRisk").GetString() ?? "Low";
                    summaryResult.KeyEvents = root.GetProperty("keyEvents").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
                    summaryResult.IsFallback = false;
                    return summaryResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama log summarization fallback triggered.");
            }

            // Fallback
            summaryResult.Summary = $"Rule-based Summary: Analyzed {logs.Count} log entries. Active operations running smoothly.";
            summaryResult.KeyEvents = logs.Take(3).Select(l => $"{l.EventType}: {l.Description}").ToList();
            summaryResult.OperationalRisk = logs.Any(l => l.EventType.Contains("Fail") || l.EventType.Contains("Alert")) ? "Medium" : "Low";
            summaryResult.IsFallback = true;
            return summaryResult;
        }

        /// <summary>
        /// Converts natural language text queries into structured log filter parameters.
        /// </summary>
        public async Task<AILogFilterResponse> ParseNaturalLanguageLogQueryAsync(string userQuery)
        {
            var response = new AILogFilterResponse { NaturalQuery = userQuery };

            if (string.IsNullOrWhiteSpace(userQuery))
            {
                response.Explanation = "Empty query provided.";
                return response;
            }

            try
            {
                var prompt = $@"Convert this user natural language log search query into structured search filter JSON.
Known EventTypes: FailedLogin, LoginSuccess, SessionTerminated, SessionStart, SessionEnd, SecurityAlert, System.

User Query: ""{userQuery}""

Respond ONLY with a raw valid JSON object without markdown formatting:
{{
  ""eventType"": ""SessionTerminated"",
  ""username"": ""customer1"",
  ""ipAddress"": null,
  ""searchKeyword"": ""killed"",
  ""explanation"": ""Filtering for forced terminations involving customer1.""
}}";

                var jsonResult = await CallOllamaGenerateAsync(prompt);
                if (!string.IsNullOrWhiteSpace(jsonResult))
                {
                    using var doc = JsonDocument.Parse(ExtractJson(jsonResult));
                    var root = doc.RootElement;
                    response.EventType = root.TryGetProperty("eventType", out var e) && e.ValueKind != JsonValueKind.Null ? e.GetString() : null;
                    response.Username = root.TryGetProperty("username", out var u) && u.ValueKind != JsonValueKind.Null ? u.GetString() : null;
                    response.IPAddress = root.TryGetProperty("ipAddress", out var ip) && ip.ValueKind != JsonValueKind.Null ? ip.GetString() : null;
                    response.SearchKeyword = root.TryGetProperty("searchKeyword", out var k) && k.ValueKind != JsonValueKind.Null ? k.GetString() : userQuery;
                    response.Explanation = root.TryGetProperty("explanation", out var ex) ? ex.GetString() ?? "" : "AI extracted search criteria.";
                    response.IsParsed = true;
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama text-to-filter fallback triggered.");
            }

            // Fallback: use raw query as search keyword
            response.SearchKeyword = userQuery;
            response.Explanation = "Raw text search (AI engine offline).";
            response.IsParsed = false;
            return response;
        }

        /// <summary>
        /// Extends client risk assessment using Ollama AI to provide deep explanations and confidence scores.
        /// </summary>
        public async Task<RiskAssessmentDto> AnalyzeClientRiskWithAiAsync(string clientId, RiskAssessmentDto ruleAssessment, List<ActivityEventDto> recentEvents)
        {
            try
            {
                var eventsSummary = string.Join("\n", recentEvents.Take(15).Select(e => $"[{e.TimestampUtc:HH:mm:ss}] Type: {e.EventType}, Sev: {e.Severity}, Msg: {e.Message}"));

                var prompt = $@"You are a Cybersecurity AI Intelligence Specialist.
Analyze client machine risk for PC '{clientId}':
Deterministic Rule Risk Score: {ruleAssessment.RiskScore}/100
Deterministic Risk Level: {ruleAssessment.RiskLevel}

Recent Activity Events:
{eventsSummary}

Respond ONLY with a raw valid JSON object without markdown formatting:
{{
  ""aiRiskScore"": 65,
  ""aiRiskLevel"": ""High"",
  ""explanation"": ""Elevated risk due to sequence of authentication anomalies."",
  ""recommendedAction"": ""Lock client PC and request administrator verification."",
  ""confidence"": 0.92
}}";

                var jsonResult = await CallOllamaGenerateAsync(prompt);
                if (!string.IsNullOrWhiteSpace(jsonResult))
                {
                    using var doc = JsonDocument.Parse(ExtractJson(jsonResult));
                    var root = doc.RootElement;
                    int aiScore = root.TryGetProperty("aiRiskScore", out var s) ? Math.Clamp(s.GetInt32(), 0, 100) : ruleAssessment.RiskScore;
                    string aiLevel = root.TryGetProperty("aiRiskLevel", out var l) ? l.GetString() ?? ruleAssessment.RiskLevel : ruleAssessment.RiskLevel;
                    string explanation = root.TryGetProperty("explanation", out var e) ? e.GetString() ?? "" : "";
                    string action = root.TryGetProperty("recommendedAction", out var a) ? a.GetString() ?? ruleAssessment.RecommendedAction : ruleAssessment.RecommendedAction;
                    double confidence = root.TryGetProperty("confidence", out var c) ? Math.Clamp(c.GetDouble(), 0.1, 1.0) : 0.95;

                    ruleAssessment.RiskScore = (ruleAssessment.RiskScore + aiScore) / 2; // Hybrid score
                    ruleAssessment.RiskLevel = ruleAssessment.RiskScore >= 80 ? "Critical" : ruleAssessment.RiskScore >= 60 ? "High" : ruleAssessment.RiskScore >= 30 ? "Medium" : "Low";
                    if (!string.IsNullOrWhiteSpace(explanation))
                        ruleAssessment.Reasons.Add($"[AI Insight] {explanation}");
                    ruleAssessment.RecommendedAction = action;
                    ruleAssessment.EvaluatedBy = $"OllamaAI ({_defaultModel})";
                    ruleAssessment.ConfidenceScore = confidence;

                    return ruleAssessment;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama AI risk analysis fallback triggered for PC {ClientId}", clientId);
            }

            ruleAssessment.EvaluatedBy = "FallbackRule";
            return ruleAssessment;
        }

        /// <summary>
        /// Converts natural language request into a draft security rule structure.
        /// </summary>
        public async Task<SecurityRuleDto> GenerateNaturalLanguageRuleDraftAsync(string userPrompt)
        {
            var draftRule = new SecurityRuleDto
            {
                RuleName = "Draft Rule",
                Description = userPrompt,
                IsDraft = true,
                IsActive = false,
                CreatedBy = "AI Assistant"
            };

            try
            {
                var prompt = $@"Convert this admin security policy prompt into a structured security rule JSON draft.
Supported Scope: SamePC, SameUser, SameSession, SameIP, SelectedPCs, AllPCs
Supported MetricTypes: FailedLogin, SecurityAlert, UnauthorizedProcess, SessionTerminated, OfflineUnexpected
Supported ActionTypes: CreateAlert, NotifyAdmin, RequestAiAnalysis, MarkClientUnderReview, LockClient, PauseSession, TerminateSession

User Policy Prompt: ""{userPrompt}""

Respond ONLY with a raw valid JSON object without markdown formatting:
{{
  ""ruleName"": ""Prevent Brute Force Logins"",
  ""description"": ""Triggers alert on repeated failed login attempts."",
  ""severity"": ""High"",
  ""scope"": ""SamePC"",
  ""metricType"": ""FailedLogin"",
  ""thresholdValue"": ""5"",
  ""windowMinutes"": 10,
  ""actionType"": ""CreateAlert"",
  ""requiresApproval"": false
}}";

                var jsonResult = await CallOllamaGenerateAsync(prompt);
                if (!string.IsNullOrWhiteSpace(jsonResult))
                {
                    using var doc = JsonDocument.Parse(ExtractJson(jsonResult));
                    var root = doc.RootElement;

                    draftRule.RuleName = root.TryGetProperty("ruleName", out var n) ? n.GetString() ?? "Draft Security Rule" : "Draft Security Rule";
                    draftRule.Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? userPrompt : userPrompt;
                    draftRule.Severity = root.TryGetProperty("severity", out var s) ? s.GetString() ?? "Medium" : "Medium";
                    draftRule.Scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() ?? "SamePC" : "SamePC";

                    string metric = root.TryGetProperty("metricType", out var m) ? m.GetString() ?? "FailedLogin" : "FailedLogin";
                    string threshold = root.TryGetProperty("thresholdValue", out var t) ? t.GetString() ?? "5" : "5";
                    int window = root.TryGetProperty("windowMinutes", out var w) ? w.GetInt32() : 10;
                    string action = root.TryGetProperty("actionType", out var a) ? a.GetString() ?? "CreateAlert" : "CreateAlert";
                    bool approval = root.TryGetProperty("requiresApproval", out var req) && req.GetBoolean();

                    draftRule.Conditions.Add(new RuleConditionDto
                    {
                        MetricType = metric,
                        Operator = "CountInWindow",
                        ThresholdValue = threshold,
                        WindowMinutes = window
                    });

                    draftRule.Actions.Add(new RuleActionDto
                    {
                        ActionType = action,
                        RequiresApproval = approval
                    });

                    return draftRule;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama NL rule assistant fallback triggered.");
            }

            // Fallback draft rule
            draftRule.RuleName = "Draft Policy Rule (Rule-based Fallback)";
            draftRule.Conditions.Add(new RuleConditionDto { MetricType = "SecurityAlert", Operator = "CountInWindow", ThresholdValue = "3", WindowMinutes = 10 });
            draftRule.Actions.Add(new RuleActionDto { ActionType = "CreateAlert", RequiresApproval = false });
            return draftRule;
        }

        private async Task<string?> CallOllamaGenerateAsync(string prompt)
        {
            var requestBody = new
            {
                model = _defaultModel,
                prompt = prompt,
                stream = false,
                format = "json"
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content);
            if (!response.IsSuccessStatusCode)
                return null;

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("response", out var respProp))
            {
                return respProp.GetString();
            }
            return null;
        }

        private string ExtractJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "{}";
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return text.Substring(start, end - start + 1);
            }
            return text;
        }
    }
}
