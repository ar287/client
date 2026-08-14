using System.Net.Http;
using System.Net.Http.Json;
using SessionManagement.Shared.DTOs;
using SessionManagement.Shared.Models;

namespace SessionManagement.Admin.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "http://localhost:5102";

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl)
            };
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                HttpResponseMessage response = await _httpClient
                    .PostAsJsonAsync("/api/auth/login", request);

                return await response.Content
                    .ReadFromJsonAsync<LoginResponse>();
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Could not connect to server: {ex.Message}"
                };
            }
        }

        public async Task<EndSessionResponse?> ForceEndSessionAsync(
            int sessionId, string reason)
        {
            try
            {
                var request = new EndSessionRequest
                {
                    SessionId = sessionId,
                    Reason    = reason
                };

                HttpResponseMessage response = await _httpClient
                    .PostAsJsonAsync("/api/session/end", request);

                return await response.Content
                    .ReadFromJsonAsync<EndSessionResponse>();
            }
            catch (Exception ex)
            {
                return new EndSessionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<BillingResponse?> GetAllBillingAsync()
        {
            try
            {
                HttpResponseMessage response = await _httpClient
                    .GetAsync("/api/billing/all");

                return await response.Content
                    .ReadFromJsonAsync<BillingResponse>();
            }
            catch (Exception ex)
            {
                return new BillingResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<bool> MarkAsPaidAsync(int billingId)
        {
            try
            {
                HttpResponseMessage response = await _httpClient
                    .PutAsync($"/api/billing/markpaid/{billingId}", null);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<EndSessionResponse?> TerminateSessionAsync(
            int sessionId, string reason)
        {
            try
            {
                var request = new EndSessionRequest
                {
                    SessionId = sessionId,
                    Reason    = reason
                };

                HttpResponseMessage response = await _httpClient
                    .PostAsJsonAsync("/api/termination/terminate", request);

                return await response.Content
                    .ReadFromJsonAsync<EndSessionResponse>();
            }
            catch (Exception ex)
            {
                return new EndSessionResponse
                {
                    Success = false,
                    Message = $"Termination error: {ex.Message}"
                };
            }
        }

        public async Task<bool> ExtendSessionAsync(int sessionId, int additionalMinutes)
        {
            try
            {
                var request = new ApproveExtensionRequest
                {
                    SessionId = sessionId,
                    AdditionalMinutes = additionalMinutes
                };

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    "/api/session/extend", request);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ── CUSTOMER MANAGEMENT ──────────────────────────────────────────

        public async Task<CustomerListResponse?> GetCustomersAsync(
            string? search = null,
            string? status = null)
        {
            try
            {
                string url = "/api/customer/all";
                var query  = new List<string>();

                if (!string.IsNullOrWhiteSpace(search))
                    query.Add($"search={Uri.EscapeDataString(search)}");
                if (!string.IsNullOrWhiteSpace(status))
                    query.Add($"status={Uri.EscapeDataString(status)}");
                if (query.Count > 0)
                    url += "?" + string.Join("&", query);

                return await _httpClient
                    .GetFromJsonAsync<CustomerListResponse>(url);
            }
            catch (Exception ex)
            {
                return new CustomerListResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<CustomerActionResponse?> CreateCustomerAsync(
            CreateCustomerRequest request)
        {
            try
            {
                var response = await _httpClient
                    .PostAsJsonAsync("/api/customer/create", request);
                return await response.Content
                    .ReadFromJsonAsync<CustomerActionResponse>();
            }
            catch (Exception ex)
            {
                return new CustomerActionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<CustomerActionResponse?> UpdateCustomerAsync(
            UpdateCustomerRequest request)
        {
            try
            {
                var response = await _httpClient
                    .PutAsJsonAsync("/api/customer/update", request);
                return await response.Content
                    .ReadFromJsonAsync<CustomerActionResponse>();
            }
            catch (Exception ex)
            {
                return new CustomerActionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<CustomerActionResponse?> ToggleCustomerStatusAsync(
            int userId)
        {
            try
            {
                var response = await _httpClient
                    .PutAsync($"/api/customer/togglestatus/{userId}", null);
                return await response.Content
                    .ReadFromJsonAsync<CustomerActionResponse>();
            }
            catch (Exception ex)
            {
                return new CustomerActionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<CustomerActionResponse?> DeleteCustomerAsync(
            int userId)
        {
            try
            {
                var response = await _httpClient
                    .DeleteAsync($"/api/customer/delete/{userId}");
                return await response.Content
                    .ReadFromJsonAsync<CustomerActionResponse>();
            }
            catch (Exception ex)
            {
                return new CustomerActionResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<LogsResponse?> GetLogsAsync(
            string? search    = null,
            string? eventType = null,
            string? username  = null,
            string? dateFrom  = null,
            string? dateTo    = null,
            int     limit     = 200)
        {
            try
            {
                var query = new List<string>();

                if (!string.IsNullOrWhiteSpace(search))
                    query.Add(
                        $"search={Uri.EscapeDataString(search)}");
                if (!string.IsNullOrWhiteSpace(eventType))
                    query.Add(
                        $"eventType={Uri.EscapeDataString(eventType)}");
                if (!string.IsNullOrWhiteSpace(username))
                    query.Add(
                        $"username={Uri.EscapeDataString(username)}");
                if (!string.IsNullOrWhiteSpace(dateFrom))
                    query.Add(
                        $"dateFrom={Uri.EscapeDataString(dateFrom)}");
                if (!string.IsNullOrWhiteSpace(dateTo))
                    query.Add(
                        $"dateTo={Uri.EscapeDataString(dateTo)}");

                query.Add($"limit={limit}");

                string url = "/api/log/all?" + string.Join("&", query);

                return await _httpClient
                    .GetFromJsonAsync<LogsResponse>(url);
            }
            catch (Exception ex)
            {
                return new LogsResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<Dictionary<string, int>?> GetLogStatsAsync()
        {
            try
            {
                return await _httpClient
                    .GetFromJsonAsync<Dictionary<string, int>>(
                        "/api/log/stats");
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> ClearOldLogsAsync(int daysOld = 30)
        {
            try
            {
                var response = await _httpClient
                    .DeleteAsync($"/api/log/clear?daysOld={daysOld}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ActiveSessionsResponse?> GetActiveSessionsAsync()
        {
            try
            {
                return await _httpClient
                    .GetFromJsonAsync<ActiveSessionsResponse>(
                        "/api/sessionquery/active");
            }
            catch (Exception ex)
            {
                return new ActiveSessionsResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        public async Task<SessionDetailDto?> GetSessionDetailAsync(
            int sessionId)
        {
            try
            {
                return await _httpClient
                    .GetFromJsonAsync<SessionDetailDto>(
                        $"/api/sessionquery/detail/{sessionId}");
            }
            catch
            {
                return null;
            }
        }

        public async Task<byte[]?> GetSessionImageAsync(string imagePath)
        {
            try
            {
                string url = $"http://localhost:5102{imagePath}";
                return await _httpClient.GetByteArrayAsync(url);
            }
            catch
            {
                return null;
            }
        }

        // ── AI INTEGRATION ENDPOINTS ──────────────────────────────────────

        public async Task<SecurityAlert?> AnalyzeSecurityAlertWithAIAsync(int alertId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/security/alerts/{alertId}/ai-analyze", null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<SecurityAlert>();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<AILogSummaryDto?> GenerateAILogSummaryAsync(int limit = 50)
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/log/ai-summary?limit={limit}", null);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AILogSummaryDto>();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<AILogFilterResponse?> ParseNaturalLanguageLogQueryAsync(string userQuery)
        {
            try
            {
                var request = new AILogFilterRequest { UserQuery = userQuery };
                var response = await _httpClient.PostAsJsonAsync("/api/log/ai-parse-query", request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AILogFilterResponse>();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
