namespace RestDb.McpServer.Classes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class RestMcpRestProxy : IDisposable
    {
        private readonly HttpClient _HttpClient;
        private readonly string _BaseUrl;
        private readonly string _ApiKeyHeader;
        private readonly string? _ApiKey;
        private readonly string? _BearerToken;

        public RestMcpRestProxy(RestMcpServerSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrWhiteSpace(settings.RestDbServerUrl)) throw new ArgumentNullException(nameof(settings.RestDbServerUrl));

            _BaseUrl = settings.RestDbServerUrl.TrimEnd('/');
            _ApiKeyHeader = String.IsNullOrWhiteSpace(settings.ApiKeyHeader) ? "x-api-key" : settings.ApiKeyHeader;
            _ApiKey = String.IsNullOrWhiteSpace(settings.ApiKey) ? null : settings.ApiKey;
            _BearerToken = String.IsNullOrWhiteSpace(settings.BearerToken) ? null : settings.BearerToken;
            _HttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        public async Task<RestMcpResponse> SendAsync(HttpMethod method, string pathAndQuery, string? jsonBody, CancellationToken token)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (String.IsNullOrWhiteSpace(pathAndQuery)) throw new ArgumentNullException(nameof(pathAndQuery));

            string url = _BaseUrl + "/" + pathAndQuery.TrimStart('/');

            using HttpRequestMessage request = new HttpRequestMessage(method, url);

            if (!String.IsNullOrWhiteSpace(_BearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _BearerToken);
            }
            else if (!String.IsNullOrWhiteSpace(_ApiKey))
            {
                request.Headers.TryAddWithoutValidation(_ApiKeyHeader, _ApiKey);
            }

            if (jsonBody != null)
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await _HttpClient.SendAsync(request, token).ConfigureAwait(false);
            string body = response.Content != null
                ? await response.Content.ReadAsStringAsync(token).ConfigureAwait(false)
                : String.Empty;

            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
            {
                headers[header.Key] = String.Join(", ", header.Value);
            }

            if (response.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
                {
                    headers[header.Key] = String.Join(", ", header.Value);
                }
            }

            return new RestMcpResponse
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase ?? String.Empty,
                Headers = headers,
                Body = ParseBody(body, headers.TryGetValue("Content-Type", out string? contentType) ? contentType : null)
            };
        }

        public static string Escape(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return Uri.EscapeDataString(value);
        }

        public void Dispose()
        {
            _HttpClient.Dispose();
        }

        private static object? ParseBody(string body, string? contentType)
        {
            if (String.IsNullOrWhiteSpace(body)) return null;

            if (!String.IsNullOrWhiteSpace(contentType)
                && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(body);
                    return document.RootElement.Clone();
                }
                catch
                {
                    return body;
                }
            }

            return body;
        }
    }

    internal sealed class RestMcpResponse
    {
        public bool Success { get; set; } = false;

        public int StatusCode { get; set; }

        public string ReasonPhrase { get; set; } = String.Empty;

        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public object? Body { get; set; } = null;
    }
}
