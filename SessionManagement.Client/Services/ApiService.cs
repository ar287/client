using System.Net.Http;
using System.Net.Http.Json;
using SessionManagement.Shared;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Client.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(AppConfig.BaseUrl)
            };
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    "/api/auth/login",
                    request
                );

                // Read response regardless of status code
                LoginResponse? result = await response.Content
                    .ReadFromJsonAsync<LoginResponse>();

                return result;
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Unable to connect to Session Server ({AppConfig.BaseUrl}). Please make sure the Server PC is running and both PCs are connected to the same network.\nDetails: {ex.Message}"
                };
            }
        }

        public async Task<ImageUploadResponse?> UploadImageAsync(
            int userId, int sessionId, byte[] imageData)
        {
            try
            {
                var request = new ImageUploadRequest
                {
                    UserId    = userId,
                    SessionId = sessionId,
                    ImageData = imageData
                };

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    "/api/image/upload",
                    request
                );

                return await response.Content
                    .ReadFromJsonAsync<ImageUploadResponse>();
            }
            catch (Exception ex)
            {
                return new ImageUploadResponse
                {
                    Success = false,
                    Message = $"Upload failed: {ex.Message}"
                };
            }
        }

        public async Task<StartSessionResponse?> StartSessionAsync(
            int userId, int allocatedMinutes)
        {
            try
            {
                var request = new StartSessionRequest
                {
                    UserId           = userId,
                    AllocatedMinutes = allocatedMinutes,
                    ClientMachine    = Environment.MachineName
                };

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    "/api/session/start", request);

                return await response.Content
                    .ReadFromJsonAsync<StartSessionResponse>();
            }
            catch (Exception ex)
            {
                return new StartSessionResponse
                {
                    Success = false,
                    Message = $"Could not start session: {ex.Message}"
                };
            }
        }

        public async Task<EndSessionResponse?> EndSessionAsync(
            int sessionId, string reason = "Completed")
        {
            try
            {
                var request = new EndSessionRequest
                {
                    SessionId = sessionId,
                    Reason    = reason
                };

                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    "/api/session/end", request);

                return await response.Content
                    .ReadFromJsonAsync<EndSessionResponse>();
            }
            catch (Exception ex)
            {
                return new EndSessionResponse
                {
                    Success = false,
                    Message = $"Could not end session: {ex.Message}"
                };
            }
        }
        public async Task<BillingResponse?> GetMyBillingAsync(int userId)
        {
            try
            {
                HttpResponseMessage response = await _httpClient
                    .GetAsync($"/api/billing/user/{userId}");

                return await response.Content
                    .ReadFromJsonAsync<BillingResponse>();
            }
            catch (Exception ex)
            {
                return new BillingResponse
                {
                    Success = false,
                    Message = $"Connection error: {ex.Message}"
                };
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
    }
}
