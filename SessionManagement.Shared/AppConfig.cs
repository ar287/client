using System;
using System.IO;
using System.Text.Json;

namespace SessionManagement.Shared
{
    public static class AppConfig
    {
        private static string? _baseUrl;

        public static string BaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_baseUrl))
                {
                    _baseUrl = LoadServerBaseUrl();
                }
                return _baseUrl;
            }
            set => _baseUrl = value?.TrimEnd('/');
        }

        public static string HubUrl => $"{BaseUrl.TrimEnd('/')}/sessionhub";

        private static string LoadServerBaseUrl()
        {
            try
            {
                string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("Server", out var serverElem) &&
                        serverElem.TryGetProperty("BaseUrl", out var baseUrlElem))
                    {
                        string? url = baseUrlElem.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            return url.TrimEnd('/');
                        }
                    }

                    if (doc.RootElement.TryGetProperty("BaseUrl", out var topBaseUrlElem))
                    {
                        string? url = topBaseUrlElem.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            return url.TrimEnd('/');
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors and fallback
            }

            string? envUrl = Environment.GetEnvironmentVariable("SESSION_SERVER_URL");
            if (!string.IsNullOrWhiteSpace(envUrl))
            {
                return envUrl.TrimEnd('/');
            }

            return "http://localhost:5102";
        }
    }
}
