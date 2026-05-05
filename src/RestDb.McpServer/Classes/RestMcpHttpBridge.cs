namespace RestDb.McpServer.Classes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic;

    internal sealed class RestMcpHttpBridge : IDisposable
    {
        private readonly string _Hostname;
        private readonly int _Port;
        private readonly string _InnerBaseUrl;
        private readonly McpHttpServer _InnerServer;
        private readonly HttpClient _HttpClient;
        private readonly Dictionary<string, string> _CorsHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Access-Control-Allow-Origin", "*" },
            { "Access-Control-Allow-Methods", "POST, GET, DELETE, OPTIONS" },
            { "Access-Control-Allow-Headers", "*" },
            { "Access-Control-Expose-Headers", "Mcp-Session-Id" },
            { "Access-Control-Max-Age", "86400" }
        };

        private HttpListener? _Listener;
        private CancellationTokenSource? _TokenSource;
        private bool _IsDisposed;
        private bool _IsStopping;

        internal event EventHandler<string>? Log;

        internal RestMcpHttpBridge(string hostname, int port, string innerBaseUrl, McpHttpServer innerServer)
        {
            if (String.IsNullOrWhiteSpace(hostname)) throw new ArgumentNullException(nameof(hostname));
            if (port < 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (String.IsNullOrWhiteSpace(innerBaseUrl)) throw new ArgumentNullException(nameof(innerBaseUrl));
            _InnerServer = innerServer ?? throw new ArgumentNullException(nameof(innerServer));

            _Hostname = hostname;
            _Port = port;
            _InnerBaseUrl = innerBaseUrl.TrimEnd('/');
            _HttpClient = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        internal async Task StartAsync(CancellationToken token = default)
        {
            _Listener = new HttpListener();
            _Listener.Prefixes.Add($"http://{_Hostname}:{_Port}/");
            _Listener.Start();
            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

            LogMessage($"HTTP bridge started on port {_Port}");
            LogMessage("Bridge paths: /mcp, /rpc, /events");

            while (!_TokenSource.IsCancellationRequested)
            {
                HttpListenerContext? context = await AcceptContextAsync(_TokenSource.Token).ConfigureAwait(false);
                if (context != null)
                {
                    _ = Task.Run(() => HandleRequestAsync(context, _TokenSource.Token));
                }
            }
        }

        internal void Stop()
        {
            if (_IsStopping) return;
            _IsStopping = true;

            try
            {
                _TokenSource?.Cancel();
            }
            catch
            {
            }

            try
            {
                if (_Listener != null && _Listener.IsListening)
                {
                    _Listener.Stop();
                }
            }
            catch
            {
            }

            LogMessage("HTTP bridge stopped");
        }

        public void Dispose()
        {
            if (_IsDisposed) return;
            _IsDisposed = true;
            Stop();
            _TokenSource?.Dispose();
            _Listener?.Close();
            _HttpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        private async Task<HttpListenerContext?> AcceptContextAsync(CancellationToken token)
        {
            try
            {
                if (_Listener == null || !_Listener.IsListening || token.IsCancellationRequested) return null;
                return await _Listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            catch (HttpListenerException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
        {
            try
            {
                string path = NormalizePath(context.Request.Url?.AbsolutePath);
                string method = context.Request.HttpMethod ?? "GET";

                if (String.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    WriteCorsHeaders(context.Response);
                    context.Response.StatusCode = 204;
                    context.Response.Close();
                    return;
                }

                if (path == "/")
                {
                    await HandleHealthAsync(context, token).ConfigureAwait(false);
                    return;
                }

                if (path == "/mcp")
                {
                    await HandleMcpAsync(context, token).ConfigureAwait(false);
                    return;
                }

                if (path == "/rpc")
                {
                    if (!String.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = 405;
                        context.Response.Close();
                        return;
                    }

                    await ProxyBufferedAsync(context, HttpMethod.Post, "/rpc", token).ConfigureAwait(false);
                    return;
                }

                if (path == "/events")
                {
                    if (!String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = 405;
                        context.Response.Close();
                        return;
                    }

                    await ProxySseAsync(context, "/events", sendPrelude: true, token).ConfigureAwait(false);
                    return;
                }

                context.Response.StatusCode = 404;
                context.Response.Close();
            }
            catch (Exception ex)
            {
                LogMessage("Bridge request error: " + ex.Message);
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                }
            }
        }

        private async Task HandleHealthAsync(HttpListenerContext context, CancellationToken token)
        {
            WriteCorsHeaders(context.Response);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            byte[] body = System.Text.Encoding.UTF8.GetBytes("{\"status\":\"Ok\"}");
            context.Response.ContentLength64 = body.Length;
            await context.Response.OutputStream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
            context.Response.Close();
        }

        private async Task HandleMcpAsync(HttpListenerContext context, CancellationToken token)
        {
            string method = context.Request.HttpMethod ?? "GET";

            if (String.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await HandleMcpPostAsync(context, token).ConfigureAwait(false);
                return;
            }

            if (String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await ProxySseAsync(context, "/events", sendPrelude: true, token).ConfigureAwait(false);
                return;
            }

            if (String.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase))
            {
                string? sessionId = GetSessionId(context.Request);
                if (!String.IsNullOrWhiteSpace(sessionId))
                {
                    _InnerServer.RemoveSession(sessionId);
                    LogMessage("Removed MCP session " + sessionId);
                }

                WriteCorsHeaders(context.Response);
                context.Response.StatusCode = 200;
                context.Response.Close();
                return;
            }

            context.Response.StatusCode = 405;
            context.Response.Close();
        }

        private async Task HandleMcpPostAsync(HttpListenerContext context, CancellationToken token)
        {
            string requestBody = String.Empty;
            if (context.Request.HasEntityBody)
            {
                using StreamReader reader = new StreamReader(
                    context.Request.InputStream,
                    context.Request.ContentEncoding ?? Encoding.UTF8);
                requestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            JsonRpcEnvelopeKind requestKind = ClassifyJsonRpcEnvelope(requestBody);

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _InnerBaseUrl + "/rpc");
            CopyRequestHeaders(context.Request, request);
            request.Content = new StringContent(
                requestBody,
                context.Request.ContentEncoding ?? Encoding.UTF8,
                GetContentMediaType(context.Request.ContentType));

            using HttpResponseMessage response = await _HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseContentRead,
                token).ConfigureAwait(false);

            if (requestKind == JsonRpcEnvelopeKind.NotificationOrResponseOnly && response.IsSuccessStatusCode)
            {
                WriteCorsHeaders(context.Response);
                CopySessionHeader(response, context.Response);
                context.Response.StatusCode = 202;
                context.Response.ContentLength64 = 0;
                context.Response.Close();
                return;
            }

            await RelayBufferedResponseAsync(context.Response, response, token).ConfigureAwait(false);
        }

        private async Task ProxyBufferedAsync(HttpListenerContext context, HttpMethod method, string innerPath, CancellationToken token)
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, _InnerBaseUrl + innerPath);
            CopyRequestHeaders(context.Request, request);

            if (context.Request.HasEntityBody)
            {
                using StreamReader reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? System.Text.Encoding.UTF8);
                string body = await reader.ReadToEndAsync().ConfigureAwait(false);
                request.Content = new StringContent(
                    body,
                    context.Request.ContentEncoding ?? Encoding.UTF8,
                    GetContentMediaType(context.Request.ContentType));
            }

            using HttpResponseMessage response = await _HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
            await RelayBufferedResponseAsync(context.Response, response, token).ConfigureAwait(false);
        }

        private async Task ProxySseAsync(HttpListenerContext context, string innerPath, bool sendPrelude, CancellationToken token)
        {
            string? sessionId = GetSessionId(context.Request);
            if (String.IsNullOrWhiteSpace(sessionId) || !IsActiveSession(sessionId))
            {
                await WriteInvalidSessionResponseAsync(context.Response, token).ConfigureAwait(false);
                return;
            }

            WriteCorsHeaders(context.Response);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/event-stream";
            context.Response.AddHeader("Cache-Control", "no-cache");
            context.Response.AddHeader("Connection", "keep-alive");
            context.Response.SendChunked = true;

            using CancellationTokenSource relayTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            using SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
            Task relayTask = RelayInnerSseAsync(context.Request, context.Response, innerPath, writeLock, relayTokenSource.Token);

            try
            {
                if (sendPrelude)
                {
                    await WriteStreamingBytesAsync(
                        context.Response,
                        System.Text.Encoding.UTF8.GetBytes(": connected\n\n"),
                        writeLock,
                        token).ConfigureAwait(false);
                }

                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);

                    await WriteStreamingBytesAsync(
                        context.Response,
                        System.Text.Encoding.UTF8.GetBytes(": keep-alive\n\n"),
                        writeLock,
                        token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogMessage("Bridge SSE error: " + ex.Message);
            }
            finally
            {
                relayTokenSource.Cancel();

                try
                {
                    await relayTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    LogMessage("Bridge SSE relay error: " + ex.Message);
                }

                context.Response.Close();
            }
        }

        private async Task RelayBufferedResponseAsync(HttpListenerResponse output, HttpResponseMessage response, CancellationToken token)
        {
            output.StatusCode = (int)response.StatusCode;
            CopyResponseHeaders(response, output, streaming: false);

            if (response.Content != null)
            {
                byte[] body = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                output.ContentLength64 = body.Length;
                if (body.Length > 0)
                {
                    await output.OutputStream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
                }
            }

            output.Close();
        }

        private static void CopySessionHeader(HttpResponseMessage source, HttpListenerResponse target)
        {
            if (source.Headers.TryGetValues("Mcp-Session-Id", out IEnumerable<string>? values))
            {
                string? sessionId = values.FirstOrDefault();
                if (!String.IsNullOrWhiteSpace(sessionId))
                {
                    target.Headers["Mcp-Session-Id"] = sessionId;
                }
            }
        }

        private void CopyRequestHeaders(HttpListenerRequest source, HttpRequestMessage target)
        {
            foreach (string? headerName in source.Headers.AllKeys)
            {
                if (String.IsNullOrWhiteSpace(headerName)) continue;
                if (ShouldSkipRequestHeader(headerName)) continue;

                string? value = source.Headers[headerName];
                if (String.IsNullOrWhiteSpace(value)) continue;

                if (!target.Headers.TryAddWithoutValidation(headerName, value))
                {
                    target.Content ??= new ByteArrayContent(Array.Empty<byte>());
                    target.Content.Headers.TryAddWithoutValidation(headerName, value);
                }
            }
        }

        private void CopyResponseHeaders(HttpResponseMessage source, HttpListenerResponse target, bool streaming)
        {
            WriteCorsHeaders(target);

            foreach (KeyValuePair<string, IEnumerable<string>> header in source.Headers)
            {
                if (ShouldSkipResponseHeader(header.Key, streaming)) continue;
                target.AddHeader(header.Key, String.Join(", ", header.Value));
            }

            if (source.Content != null)
            {
                foreach (KeyValuePair<string, IEnumerable<string>> header in source.Content.Headers)
                {
                    if (ShouldSkipResponseHeader(header.Key, streaming)) continue;

                    if (String.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        target.ContentType = String.Join(", ", header.Value);
                    }
                    else
                    {
                        target.AddHeader(header.Key, String.Join(", ", header.Value));
                    }
                }
            }

            if (streaming)
            {
                target.SendChunked = true;
                if (String.IsNullOrWhiteSpace(target.ContentType))
                {
                    target.ContentType = "text/event-stream";
                }
            }
        }

        private void WriteCorsHeaders(HttpListenerResponse response)
        {
            foreach (KeyValuePair<string, string> kvp in _CorsHeaders)
            {
                response.Headers[kvp.Key] = kvp.Value;
            }
        }

        private static string NormalizePath(string? path)
        {
            if (String.IsNullOrWhiteSpace(path)) return "/";
            if (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal)) return path.TrimEnd('/');
            return path;
        }

        private static string? GetSessionId(HttpListenerRequest request)
        {
            return request.Headers["Mcp-Session-Id"] ?? request.QueryString["session"];
        }

        private bool IsActiveSession(string sessionId)
        {
            return _InnerServer.GetActiveSessions().Contains(sessionId, StringComparer.Ordinal);
        }

        private async Task RelayInnerSseAsync(
            HttpListenerRequest sourceRequest,
            HttpListenerResponse targetResponse,
            string innerPath,
            SemaphoreSlim writeLock,
            CancellationToken token)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _InnerBaseUrl + innerPath);
            CopyRequestHeaders(sourceRequest, request);

            if (!request.Headers.Accept.Any())
            {
                request.Headers.Accept.ParseAdd("text/event-stream");
            }

            using HttpResponseMessage response = await _HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = response.Content != null
                    ? await response.Content.ReadAsStringAsync(token).ConfigureAwait(false)
                    : String.Empty;

                if (!String.IsNullOrWhiteSpace(errorBody))
                {
                    LogMessage("Inner SSE endpoint returned " + (int)response.StatusCode + ": " + errorBody);
                }
                else
                {
                    LogMessage("Inner SSE endpoint returned " + (int)response.StatusCode);
                }

                return;
            }

            using Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            byte[] buffer = new byte[81920];

            while (!token.IsCancellationRequested)
            {
                int bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (bytesRead <= 0) break;

                await WriteStreamingBytesAsync(targetResponse, buffer, bytesRead, writeLock, token).ConfigureAwait(false);
            }
        }

        private async Task WriteInvalidSessionResponseAsync(HttpListenerResponse response, CancellationToken token)
        {
            WriteCorsHeaders(response);
            response.StatusCode = 400;
            response.ContentType = "text/plain";
            byte[] body = System.Text.Encoding.UTF8.GetBytes("Missing or invalid session ID. Send a POST to initialize first.");
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
            response.Close();
        }

        private async Task WriteStreamingBytesAsync(
            HttpListenerResponse response,
            byte[] buffer,
            SemaphoreSlim writeLock,
            CancellationToken token)
        {
            await WriteStreamingBytesAsync(response, buffer, buffer.Length, writeLock, token).ConfigureAwait(false);
        }

        private async Task WriteStreamingBytesAsync(
            HttpListenerResponse response,
            byte[] buffer,
            int count,
            SemaphoreSlim writeLock,
            CancellationToken token)
        {
            await writeLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await response.OutputStream.WriteAsync(buffer, 0, count, token).ConfigureAwait(false);
                await response.OutputStream.FlushAsync(token).ConfigureAwait(false);
            }
            finally
            {
                writeLock.Release();
            }
        }

        private static bool ShouldSkipRequestHeader(string headerName)
        {
            return headerName.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase)
                || headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                || headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                || headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipResponseHeader(string headerName, bool streaming)
        {
            if (headerName.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) return true;
            if (headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) return streaming;
            if (headerName.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void LogMessage(string message)
        {
            Log?.Invoke(this, message);
        }

        private static string GetContentMediaType(string? contentType)
        {
            if (String.IsNullOrWhiteSpace(contentType)) return "application/json";
            int separatorIndex = contentType.IndexOf(';');
            if (separatorIndex >= 0) contentType = contentType.Substring(0, separatorIndex);
            return String.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType.Trim();
        }

        private static JsonRpcEnvelopeKind ClassifyJsonRpcEnvelope(string body)
        {
            if (String.IsNullOrWhiteSpace(body)) return JsonRpcEnvelopeKind.Unknown;

            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                return ClassifyJsonRpcElement(document.RootElement);
            }
            catch (JsonException)
            {
                return JsonRpcEnvelopeKind.Unknown;
            }
        }

        private static JsonRpcEnvelopeKind ClassifyJsonRpcElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ClassifyJsonRpcObject(element),
                JsonValueKind.Array => ClassifyJsonRpcArray(element),
                _ => JsonRpcEnvelopeKind.Unknown
            };
        }

        private static JsonRpcEnvelopeKind ClassifyJsonRpcArray(JsonElement element)
        {
            bool sawRequest = false;
            bool sawNotificationOrResponse = false;

            foreach (JsonElement item in element.EnumerateArray())
            {
                JsonRpcEnvelopeKind itemKind = ClassifyJsonRpcObject(item);
                if (itemKind == JsonRpcEnvelopeKind.Unknown) return JsonRpcEnvelopeKind.Unknown;
                if (itemKind == JsonRpcEnvelopeKind.Request) sawRequest = true;
                if (itemKind == JsonRpcEnvelopeKind.NotificationOrResponseOnly) sawNotificationOrResponse = true;
            }

            if (sawRequest) return JsonRpcEnvelopeKind.Request;
            if (sawNotificationOrResponse) return JsonRpcEnvelopeKind.NotificationOrResponseOnly;
            return JsonRpcEnvelopeKind.Unknown;
        }

        private static JsonRpcEnvelopeKind ClassifyJsonRpcObject(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return JsonRpcEnvelopeKind.Unknown;

            bool hasMethod = element.TryGetProperty("method", out _);
            bool hasId = element.TryGetProperty("id", out _);
            bool hasResult = element.TryGetProperty("result", out _);
            bool hasError = element.TryGetProperty("error", out _);

            if (hasMethod && hasId) return JsonRpcEnvelopeKind.Request;
            if (hasMethod && !hasId) return JsonRpcEnvelopeKind.NotificationOrResponseOnly;
            if (!hasMethod && hasId && (hasResult || hasError)) return JsonRpcEnvelopeKind.NotificationOrResponseOnly;

            return JsonRpcEnvelopeKind.Unknown;
        }

        private enum JsonRpcEnvelopeKind
        {
            Unknown,
            Request,
            NotificationOrResponseOnly
        }
    }
}
