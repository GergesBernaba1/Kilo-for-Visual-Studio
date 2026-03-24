using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kilo.VisualStudio.Contracts.Models;

namespace Kilo.VisualStudio.Integration
{
    public class KiloProtocolSessionHostAdapter : KiloSessionHostAdapterBase
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly HttpClient _httpClient;
        private readonly object _gate = new object();
        private CancellationTokenSource? _streamCancellation;
        private Task? _streamTask;
        private KiloServerEndpoint _endpoint = new KiloServerEndpoint();
        private string _workspaceDirectory = string.Empty;
        private KiloConnectionState _connectionState = KiloConnectionState.Disconnected;

        public KiloProtocolSessionHostAdapter(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public override KiloConnectionState ConnectionState => _connectionState;

    public override async Task ConnectAsync(KiloServerEndpoint endpoint, string workspaceDirectory, CancellationToken cancellationToken)
    {
        if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

        // If we're already connected to the same endpoint and workspace, do nothing
        if (_connectionState == KiloConnectionState.Connected && 
            _endpoint.BaseUrl == endpoint.BaseUrl &&
            _endpoint.Username == endpoint.Username &&
            _endpoint.Password == endpoint.Password &&
            string.Equals(_workspaceDirectory, workspaceDirectory ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await DisconnectAsync(CancellationToken.None);

        _endpoint = endpoint;
        _workspaceDirectory = workspaceDirectory ?? string.Empty;

        ConfigureAuthorizationHeader();
        await PingHealthAsync(cancellationToken);

        var streamCancellation = new CancellationTokenSource();
        lock (_gate)
        {
            // Cancel any existing stream task
            _streamCancellation?.Cancel();
            _streamCancellation = streamCancellation;
            _streamTask = RunEventStreamAsync(streamCancellation.Token);
        }

        var waitUntil = DateTimeOffset.UtcNow.AddSeconds(5);
        while (_connectionState != KiloConnectionState.Connected && _connectionState != KiloConnectionState.Error)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= waitUntil)
            {
                throw new TimeoutException("Timed out waiting for the Kilo event stream to connect.");
            }

            await Task.Delay(50, cancellationToken);
        }

        if (_connectionState == KiloConnectionState.Error)
        {
            throw new InvalidOperationException("Kilo event stream entered an error state during connect.");
        }
    }

        public override async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource? streamCancellation;
            Task? streamTask;

            lock (_gate)
            {
                streamCancellation = _streamCancellation;
                streamTask = _streamTask;
                _streamCancellation = null;
                _streamTask = null;
            }

            if (streamCancellation != null)
            {
                streamCancellation.Cancel();
                streamCancellation.Dispose();
            }

            if (streamTask != null)
            {
                try
                {
                    await streamTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            SetConnectionState(KiloConnectionState.Disconnected, "Kilo event stream disconnected.");
            cancellationToken.ThrowIfCancellationRequested();
        }

        public override async Task<KiloSessionSummary> CreateSessionAsync(string workspaceDirectory, CancellationToken cancellationToken)
        {
            using var response = await SendScopedAsync(HttpMethod.Post, "/session", workspaceDirectory, payload: null, cancellationToken: cancellationToken);
            var document = await ReadJsonAsync(response);
            return ParseSessionResponse(document.RootElement);
        }

        public override async Task<IReadOnlyList<KiloSessionSummary>> ListSessionsAsync(string workspaceDirectory, CancellationToken cancellationToken)
        {
            using var response = await SendScopedAsync(HttpMethod.Get, "/session", workspaceDirectory, payload: null, cancellationToken: cancellationToken);
            var document = await ReadJsonAsync(response);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new KiloSessionSummary[0];
            }

            return document.RootElement.EnumerateArray().Select(ParseSessionResponse).ToList();
        }

        public override async Task<KiloSessionSummary?> GetSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
        {
            using var response = await SendScopedAsync(HttpMethod.Get, "/session/" + Uri.EscapeDataString(sessionId), workspaceDirectory, payload: null, cancellationToken: cancellationToken);
            var document = await ReadJsonAsync(response);
            return ParseSessionResponse(document.RootElement);
        }

        public override async Task DeleteSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
        {
            using var response = await SendScopedAsync(HttpMethod.Delete, "/session/" + Uri.EscapeDataString(sessionId), workspaceDirectory, payload: null, cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public override async Task<IReadOnlyList<KiloSessionMessage>> GetMessagesAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
        {
            using var response = await SendScopedAsync(HttpMethod.Get, "/session/" + Uri.EscapeDataString(sessionId) + "/messages", workspaceDirectory, payload: null, cancellationToken: cancellationToken);
            var document = await ReadJsonAsync(response);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new KiloSessionMessage[0];
            }

            return document.RootElement.EnumerateArray().Select(ParseMessage).ToList();
        }

        public override async Task SendPromptAsync(KiloChatRequest request, CancellationToken cancellationToken)
        {
            var payload = new
            {
                text = request.Prompt,
                activeFilePath = request.ActiveFilePath,
                selectedText = request.SelectedText,
                languageId = request.LanguageId,
                providerID = request.ProviderId,
                modelID = request.ModelId,
                agent = request.Agent,
                variant = request.Variant,
                noReply = request.NoReply,
                editorContext = new
                {
                    activeFile = request.ActiveFilePath,
                    visibleFiles = string.IsNullOrWhiteSpace(request.ActiveFilePath) ? new string[0] : new[] { request.ActiveFilePath },
                    openTabs = string.IsNullOrWhiteSpace(request.ActiveFilePath) ? new string[0] : new[] { request.ActiveFilePath }
                }
            };

            using var response = await SendScopedAsync(HttpMethod.Post, "/session/" + Uri.EscapeDataString(request.SessionId) + "/chat", request.WorkspaceDirectory, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public override async Task AbortSessionAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
        {
            using var response = await SendScopedAsync(HttpMethod.Post, "/session/" + Uri.EscapeDataString(sessionId) + "/abort", workspaceDirectory, payload: null, cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public override async Task<IReadOnlyList<KiloFileDiff>> GetSessionDiffAsync(string sessionId, string workspaceDirectory, CancellationToken cancellationToken)
        {
            using var response = await SendScopedAsync(HttpMethod.Get, "/session/" + Uri.EscapeDataString(sessionId) + "/diff", workspaceDirectory, payload: null, cancellationToken: cancellationToken);
            var document = await ReadJsonAsync(response);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new KiloFileDiff[0];
            }

            return document.RootElement.EnumerateArray().Select(ParseFileDiff).ToList();
        }

        public override async Task<IReadOnlyList<string>> GetRegisteredToolIdsAsync(string workspaceDirectory, CancellationToken cancellationToken)
        {
            using var response = await SendScopedAsync(HttpMethod.Get, "/experimental/tool/ids", workspaceDirectory, payload: null, cancellationToken: cancellationToken);
            var document = await ReadJsonAsync(response);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new string[0];
            }

            return document.RootElement.EnumerateArray().Select(element => element.GetString() ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        }

        public override async Task ReplyToToolPermissionAsync(KiloToolPermissionReply reply, string workspaceDirectory, CancellationToken cancellationToken)
        {
            var payload = new
            {
                permissionId = reply.PermissionId,
                sessionID = reply.SessionId,
                response = reply.Approved ? "approve" : "deny",
                approvedAlways = reply.ApproveAlways,
                deniedAlways = reply.DenyAlways
            };

            using var response = await SendScopedAsync(HttpMethod.Post, "/permission/reply", workspaceDirectory, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task PingHealthAsync(CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildAbsoluteUrl("/global/health"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task RunEventStreamAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SetConnectionState(KiloConnectionState.Connecting, "Connecting to the Kilo event stream.");

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, BuildScopedUrl("/event", _workspaceDirectory));
                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    SetConnectionState(KiloConnectionState.Connected, "Connected to the Kilo event stream.");

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var reader = new StreamReader(stream);
                    var dataBuilder = new StringBuilder();

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync();
                        if (line == null)
                        {
                            break;
                        }

                        if (line.Length == 0)
                        {
                            if (dataBuilder.Length > 0)
                            {
                                ProcessSsePayload(dataBuilder.ToString());
                                dataBuilder.Clear();
                            }

                            continue;
                        }

                        if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            dataBuilder.Append(line.Substring(5).Trim());
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SetConnectionState(KiloConnectionState.Error, ex.Message);
                    Publish(new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.Error,
                        EventType = "session.error",
                        Error = ex.Message,
                        Message = ex.Message,
                        TimestampUtc = DateTimeOffset.UtcNow
                    });
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(250, cancellationToken);
                }
            }
        }

        private void ProcessSsePayload(string payload)
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var eventType = GetString(root, "type");
            var properties = root.TryGetProperty("properties", out var propertiesElement) ? propertiesElement : root;
            var sessionEvent = MapEvent(eventType, properties);
            Publish(sessionEvent);
        }

        private KiloSessionEvent MapEvent(string eventType, JsonElement properties)
        {
            switch (eventType)
            {
                case "session.created":
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.SessionCreated,
                        EventType = eventType,
                        SessionId = ParseSessionResponse(properties).SessionId,
                        Session = ParseSessionResponse(properties),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "session.updated":
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.SessionUpdated,
                        EventType = eventType,
                        SessionId = ParseSessionResponse(properties).SessionId,
                        Session = ParseSessionResponse(properties),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "session.deleted":
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.SessionDeleted,
                        EventType = eventType,
                        SessionId = ParseSessionResponse(properties).SessionId,
                        Session = ParseSessionResponse(properties),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "session.turn.open":
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.TurnStarted,
                        EventType = eventType,
                        SessionId = GetString(properties, "sessionID"),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "session.turn.close":
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.TurnCompleted,
                        EventType = eventType,
                        SessionId = GetString(properties, "sessionID"),
                        Message = GetString(properties, "reason"),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "session.diff":
                    var fileDiffs = properties.TryGetProperty("diff", out var diffArray) && diffArray.ValueKind == JsonValueKind.Array
                        ? diffArray.EnumerateArray().Select(ParseFileDiff).ToList()
                        : new List<KiloFileDiff>();
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.DiffUpdated,
                        EventType = eventType,
                        SessionId = GetString(properties, "sessionID"),
                        FileDiffs = fileDiffs,
                        PatchDiff = BuildPatchDiff(fileDiffs),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "message.updated":
                    var messageInfo = properties.TryGetProperty("info", out var infoElement) ? infoElement : properties;
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.MessageUpdated,
                        EventType = eventType,
                        SessionId = GetString(messageInfo, "sessionID"),
                        MessageId = GetString(messageInfo, "id"),
                        Message = GetString(messageInfo, "text"),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "message.removed":
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.MessageRemoved,
                        EventType = eventType,
                        SessionId = GetString(properties, "sessionID"),
                        MessageId = GetString(properties, "messageID"),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "message.part.delta":
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.TextDelta,
                        EventType = eventType,
                        SessionId = GetString(properties, "sessionID"),
                        MessageId = GetString(properties, "messageID"),
                        PartId = GetString(properties, "partID"),
                        Delta = GetString(properties, "delta"),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "message.part.updated":
                    return MapPartUpdatedEvent(eventType, properties);
                case "message.part.removed":
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.PartRemoved,
                        EventType = eventType,
                        SessionId = GetString(properties, "sessionID"),
                        MessageId = GetString(properties, "messageID"),
                        PartId = GetString(properties, "partID"),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                case "session.error":
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.Error,
                        EventType = eventType,
                        SessionId = GetString(properties, "sessionID"),
                        Error = GetString(properties, "error"),
                        Message = GetString(properties, "error"),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
                default:
                    return new KiloSessionEvent
                    {
                        Kind = KiloSessionEventKind.Unknown,
                        EventType = eventType,
                        SessionId = GetString(properties, "sessionID"),
                        TimestampUtc = DateTimeOffset.UtcNow
                    };
            }
        }

        private KiloSessionEvent MapPartUpdatedEvent(string eventType, JsonElement properties)
        {
            var part = properties.TryGetProperty("part", out var partElement) ? partElement : properties;
            var partType = GetString(part, "type");
            if (string.Equals(partType, "tool", StringComparison.OrdinalIgnoreCase))
            {
                var toolExecution = ParseToolExecution(part);
                return new KiloSessionEvent
                {
                    Kind = KiloSessionEventKind.ToolExecutionUpdated,
                    EventType = eventType,
                    SessionId = GetString(part, "sessionID"),
                    MessageId = GetString(part, "messageID"),
                    PartId = GetString(part, "id"),
                    ToolExecution = toolExecution,
                    SuggestedCode = toolExecution.SuggestedCode,
                    PatchDiff = toolExecution.PatchDiff,
                    TimestampUtc = DateTimeOffset.UtcNow
                };
            }

            return new KiloSessionEvent
            {
                Kind = KiloSessionEventKind.PartUpdated,
                EventType = eventType,
                SessionId = GetString(part, "sessionID"),
                MessageId = GetString(part, "messageID"),
                PartId = GetString(part, "id"),
                Message = GetString(part, "text"),
                TimestampUtc = DateTimeOffset.UtcNow
            };
        }

        private async Task<HttpResponseMessage> SendScopedAsync(HttpMethod method, string path, string workspaceDirectory, object? payload, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(method, BuildScopedUrl(path, workspaceDirectory));
            if (payload != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return response;
        }

        private async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content);
        }

        private void ConfigureAuthorizationHeader()
        {
            var raw = (_endpoint.Username ?? "kilo") + ":" + (_endpoint.Password ?? string.Empty);
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        private string BuildScopedUrl(string path, string workspaceDirectory)
        {
            var builder = new StringBuilder();
            builder.Append(BuildAbsoluteUrl(path));

            if (!string.IsNullOrWhiteSpace(workspaceDirectory))
            {
                builder.Append(path.Contains("?", StringComparison.Ordinal) ? '&' : '?');
                builder.Append("directory=").Append(Uri.EscapeDataString(workspaceDirectory));
            }

            return builder.ToString();
        }

        private string BuildAbsoluteUrl(string path)
        {
            var baseUrl = string.IsNullOrWhiteSpace(_endpoint.BaseUrl) ? "http://127.0.0.1:4096" : _endpoint.BaseUrl.TrimEnd('/');
            var relativePath = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
            return baseUrl + relativePath;
        }

        private void SetConnectionState(KiloConnectionState state, string message)
        {
            _connectionState = state;
            PublishConnectionState(state, message);
        }

        private static KiloSessionSummary ParseSessionResponse(JsonElement element)
        {
            var info = element.TryGetProperty("info", out var infoElement) ? infoElement : element;
            return new KiloSessionSummary
            {
                SessionId = GetString(info, "id"),
                Title = GetString(info, "title"),
                WorkspaceDirectory = GetString(info, "directory"),
                Status = ParseSessionStatus(info),
                CreatedAtUtc = ParseUnixTimestamp(info, "time", "created"),
                UpdatedAtUtc = ParseUnixTimestamp(info, "time", "updated")
            };
        }

        private static KiloSessionMessage ParseMessage(JsonElement element)
        {
            var info = element.TryGetProperty("info", out var infoElement) ? infoElement : element;
            return new KiloSessionMessage
            {
                MessageId = GetString(info, "id"),
                SessionId = GetString(info, "sessionID"),
                Role = GetString(info, "role"),
                Content = GetString(info, "text"),
                CreatedAtUtc = ParseUnixTimestamp(info, "time", "created")
            };
        }

        private static KiloFileDiff ParseFileDiff(JsonElement element)
        {
            return new KiloFileDiff
            {
                FilePath = GetString(element, "file"),
                Before = GetString(element, "before"),
                After = GetString(element, "after"),
                Additions = GetInt(element, "additions"),
                Deletions = GetInt(element, "deletions"),
                Status = GetString(element, "status")
            };
        }

        private static KiloToolExecution ParseToolExecution(JsonElement part)
        {
            var execution = new KiloToolExecution
            {
                CallId = GetString(part, "callID"),
                ToolName = GetString(part, "tool"),
                Title = GetString(part, "title"),
                Status = ParseToolStatus(part),
                InputSummary = GetNestedString(part, "input", "summary"),
                OutputSummary = GetNestedString(part, "output", "summary")
            };

            if (part.TryGetProperty("state", out var stateElement))
            {
                if (stateElement.TryGetProperty("metadata", out var metadata) && metadata.TryGetProperty("filediff", out var fileDiff))
                {
                    var diffs = new List<KiloFileDiff>
                    {
                        new KiloFileDiff
                        {
                            FilePath = GetString(fileDiff, "file"),
                            Before = GetString(fileDiff, "before"),
                            After = GetString(fileDiff, "after"),
                            Additions = GetInt(fileDiff, "additions"),
                            Deletions = GetInt(fileDiff, "deletions"),
                            Status = GetString(fileDiff, "status")
                        }
                    };

                    execution.FileDiffs = diffs;
                    execution.PatchDiff = BuildPatchDiff(diffs);
                }

                execution.SuggestedCode = GetNestedString(stateElement, "output", "text");
            }

            return execution;
        }

        private static KiloSessionStatus ParseSessionStatus(JsonElement element)
        {
            var value = GetString(element, "status");
            if (string.IsNullOrWhiteSpace(value) && element.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.Object)
            {
                value = GetString(statusElement, "type");
            }

            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "idle": return KiloSessionStatus.Idle;
                case "running":
                case "busy": return KiloSessionStatus.Running;
                case "retry": return KiloSessionStatus.Retry;
                case "completed": return KiloSessionStatus.Completed;
                case "failed":
                case "error": return KiloSessionStatus.Failed;
                case "aborted":
                case "cancelled": return KiloSessionStatus.Aborted;
                default: return KiloSessionStatus.Unknown;
            }
        }

        private static KiloToolExecutionStatus ParseToolStatus(JsonElement element)
        {
            var value = GetNestedString(element, "state", "status");
            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "pending": return KiloToolExecutionStatus.Pending;
                case "running":
                case "in_progress": return KiloToolExecutionStatus.Running;
                case "completed":
                case "done": return KiloToolExecutionStatus.Completed;
                case "failed":
                case "error": return KiloToolExecutionStatus.Failed;
                case "cancelled":
                case "aborted": return KiloToolExecutionStatus.Cancelled;
                default: return KiloToolExecutionStatus.Unknown;
            }
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return string.Empty;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString() ?? string.Empty;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return value.ToString();
                default:
                    return string.Empty;
            }
        }

        private static string GetNestedString(JsonElement element, string objectName, string propertyName)
        {
            if (!element.TryGetProperty(objectName, out var nested) || nested.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            return GetString(nested, propertyName);
        }

        private static int GetInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result))
            {
                return result;
            }

            return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : 0;
        }

        private static DateTimeOffset ParseUnixTimestamp(JsonElement element, string objectName, string propertyName)
        {
            if (!element.TryGetProperty(objectName, out var timeElement) || timeElement.ValueKind != JsonValueKind.Object)
            {
                return DateTimeOffset.UtcNow;
            }

            if (!timeElement.TryGetProperty(propertyName, out var value))
            {
                return DateTimeOffset.UtcNow;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unixValue))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixValue);
            }

            return DateTimeOffset.UtcNow;
        }
    }
}
