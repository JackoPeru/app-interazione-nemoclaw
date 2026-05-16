using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed record GatewayChatResult(
    string Message,
    string Source,
    string StatusMessage,
    bool UsedFallback,
    string? ResponseId = null,
    IReadOnlyList<VisualBlockRecord>? VisualBlocks = null,
    int? VisualBlocksVersion = null);

public sealed record ServerSnapshot(
    string Gateway,
    string Model,
    string ProviderDetail,
    string InferenceEndpoint,
    string Policy,
    string StatusMessage,
    string VideoLibraryPath);

public enum TaskCommand
{
    Approve,
    Deny,
    Complete
}

public sealed record GatewayTaskResult(AgentTaskRecord Task, string Message);

public sealed record WorkspaceRunResult(string Result, string Source, string Status);

public static class GatewayService
{
    internal const string HermesFallbackApiKey = "hermes-hub";

    private static readonly HttpClientHandler HttpHandler = new()
    {
        AllowAutoRedirect = false
    };

    // Timeout default 5 min: chat non-streaming su modelli grandi puo' impiegare minuti.
    // Body cap 10 MB: protezione contro response gigante / server malevolo.
    private static readonly HttpClient HttpClient = new(HttpHandler)
    {
        Timeout = TimeSpan.FromMinutes(5),
        MaxResponseContentBufferSize = 10L * 1024 * 1024
    };

    private sealed record BufferedHermesResponse(int StatusCode, string? ReasonPhrase, string Body, string? MediaType)
    {
        public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
    }

    public static async Task<GatewayChatResult> SendChatAsync(
        AppSettings settings,
        string mode,
        string prompt,
        IReadOnlyList<ChatMessageRecord> history,
        string? conversationId = null,
        string? previousResponseId = null)
    {
        string? lastError = null;

        if (await SupportsResponsesAsync(settings))
        {
            try
            {
                var responsePayload = JsonSerializer.Serialize(new
                {
                    model = settings.Model,
                    input = prompt,
                    instructions = HermesHubProtocol.Instructions(mode),
                    store = true,
                    conversation = string.IsNullOrWhiteSpace(conversationId) ? null : conversationId,
                    previous_response_id = string.IsNullOrWhiteSpace(previousResponseId) ? null : previousResponseId,
                    metadata = HermesHubProtocol.Metadata(settings)
                });

                var response = await SendBufferedAsync(token =>
                    BuildJsonRequest(HttpMethod.Post, $"{settings.GatewayUrl.TrimEnd('/')}/responses", responsePayload, token));
                var body = response.Body;

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {response.StatusCode} {response.ReasonPhrase}";
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        lastError += $": {ExtractHumanError(body)}";
                    }
                    lastError = $"Responses API {lastError}";
                }
                else
                {
                    var assistantText = ExtractAssistantText(response.MediaType, body);
                    if (!string.IsNullOrWhiteSpace(assistantText))
                    {
                        return new GatewayChatResult(
                            assistantText,
                            "Hermes",
                            "Risposta ricevuta da Hermes Responses API.",
                            false,
                            ExtractResponseId(body),
                            VisualBlockParser.ExtractFromResponse(body),
                            VisualBlocksContract.Version);
                    }

                    lastError = "Hermes Responses API raggiunta, ma senza contenuto utile.";
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        try
        {
            var chatPayload = JsonSerializer.Serialize(new
            {
                model = settings.Model,
                stream = false,
                metadata = HermesHubProtocol.Metadata(settings),
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = HermesHubProtocol.Instructions(mode)
                    }
                }.Concat(history.Select(message => new
                {
                    role = string.Equals(message.Author, "Tu", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant",
                    content = message.Text
                }))
            });

            var response = await SendBufferedAsync(token =>
                BuildJsonRequest(HttpMethod.Post, $"{settings.GatewayUrl.TrimEnd('/')}/chat/completions", chatPayload, token));
            var body = response.Body;

            if (response.IsSuccessStatusCode)
            {
                var assistantText = ExtractAssistantText(response.MediaType, body);
                if (!string.IsNullOrWhiteSpace(assistantText))
                {
                    return new GatewayChatResult(
                        assistantText,
                        "Hermes",
                        "Risposta ricevuta da Hermes Chat Completions.",
                        false,
                        ExtractResponseId(body),
                        VisualBlockParser.ExtractFromResponse(body),
                        VisualBlocksContract.Version);
                }

                lastError = "Hermes Chat Completions raggiunta, ma senza contenuto utile.";
            }
            else
            {
                lastError = $"HTTP {response.StatusCode} {response.ReasonPhrase}: {ExtractHumanError(body)}";
            }
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
        }

        if (settings.DemoMode)
        {
            return new GatewayChatResult(
                BuildFallbackReply(settings, mode, lastError),
                "Fallback locale",
                $"Hermes non disponibile, uso fallback locale: {lastError ?? "errore sconosciuto"}.",
                true,
                null,
                VisualBlockFixtures.ShouldAttach(settings, prompt) ? VisualBlockFixtures.Create() : [],
                VisualBlocksContract.Version);
        }

        return new GatewayChatResult(
            $"Hermes non raggiungibile: {lastError ?? "errore sconosciuto"}.",
            "Errore Hermes",
            $"Invio fallito: {lastError ?? "errore sconosciuto"}.",
            false,
            null,
            VisualBlockFixtures.ShouldAttach(settings, prompt) ? VisualBlockFixtures.Create() : [],
            VisualBlocksContract.Version);
    }

    public static async Task<GatewayTaskResult> QueueTaskAsync(AppSettings settings, AgentTaskRecord task)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title = task.Title,
            instructions = task.Detail,
            detail = task.Detail,
            mode = task.Mode,
            requiresApproval = task.RequiresApproval,
            approvalRequired = task.RequiresApproval,
            model = settings.Model,
            provider = settings.Provider,
            metadata = HermesHubProtocol.Metadata(settings, source: "jobs-section")
        });

        try
        {
            var response = await SendBufferedAsync(token =>
                BuildJsonRequest(HttpMethod.Post, $"{HermesRoot(settings)}/api/jobs", payload, token));
            var body = response.Body;

            if (response.IsSuccessStatusCode)
            {
                var remoteId = ExtractTaskId(body);
                var remoteStatus = ExtractTaskStatus(body) ?? task.Status;
                var syncedTask = task with
                {
                    RemoteId = remoteId,
                    Status = remoteStatus,
                    Source = "Hermes Jobs",
                    Mode = "Job",
                    UpdatedAt = DateTimeOffset.Now
                };
                return new GatewayTaskResult(syncedTask, "Job creato su Hermes.");
            }

            var error = $"HTTP {response.StatusCode} {response.ReasonPhrase}";
            if (!string.IsNullOrWhiteSpace(body))
            {
                error += $": {ExtractHumanError(body)}";
            }

            return BuildTaskFallback(settings, task, $"Creazione task fallita: {error}");
        }
        catch (Exception ex)
        {
            return BuildTaskFallback(settings, task, $"Creazione task fallita: {ex.Message}");
        }
    }

    public static async Task<GatewayTaskResult> UpdateTaskAsync(AppSettings settings, AgentTaskRecord task, TaskCommand command)
    {
        var targetStatus = command switch
        {
            TaskCommand.Approve => "Run richiesto",
            TaskCommand.Deny => "Pausa richiesta",
            _ => "Eliminato"
        };

        if (string.IsNullOrWhiteSpace(task.RemoteId))
        {
            var localTask = task with
            {
                Status = $"{targetStatus} locale",
                Source = "Fallback locale",
                UpdatedAt = DateTimeOffset.Now
            };
            return new GatewayTaskResult(localTask, $"Job aggiornato in locale: {targetStatus}.");
        }

        var requestUri = command switch
        {
            TaskCommand.Approve => $"{HermesRoot(settings)}/api/jobs/{task.RemoteId}/run",
            TaskCommand.Deny => $"{HermesRoot(settings)}/api/jobs/{task.RemoteId}/pause",
            _ => $"{HermesRoot(settings)}/api/jobs/{task.RemoteId}"
        };
        var method = command switch
        {
            TaskCommand.Complete => HttpMethod.Delete,
            _ => HttpMethod.Post
        };

        try
        {
            var response = await SendBufferedAsync(token =>
                BuildJsonRequest(method, requestUri, "{}", token));
            var body = response.Body;

            if (response.IsSuccessStatusCode)
            {
                var remoteStatus = command == TaskCommand.Complete
                    ? "Eliminato"
                    : ExtractTaskStatus(body) ?? targetStatus;
                var syncedTask = task with
                {
                    Status = remoteStatus,
                    Source = "Hermes Jobs",
                    UpdatedAt = DateTimeOffset.Now
                };
                return new GatewayTaskResult(syncedTask, $"Job aggiornato su Hermes: {remoteStatus}.");
            }

            var error = $"HTTP {response.StatusCode} {response.ReasonPhrase}";
            if (!string.IsNullOrWhiteSpace(body))
            {
                error += $": {ExtractHumanError(body)}";
            }

            return BuildTaskFallback(settings, task with { Status = targetStatus }, $"Aggiornamento job fallito: {error}");
        }
        catch (Exception ex)
        {
            return BuildTaskFallback(settings, task with { Status = targetStatus }, $"Aggiornamento job fallito: {ex.Message}");
        }
    }

    public static async Task<WorkspaceRunResult> SendWorkspaceRunAsync(AppSettings settings, string kind, string prompt)
    {
        var runPrompt = kind.Equals("Video", StringComparison.OrdinalIgnoreCase)
            ? $"Destinazione: Hermes Hub / Video.\nCartella video monitorata: {settings.VideoLibraryPath}\nMemoria: usa la memoria agente condivisa Hermes/CLI/app per preferenze utente, stile, durata, ritmo, fonti e regole editoriali. Se impari una preferenza stabile, salvala lato Hermes se possibile.\nObiettivo: crea output operativo per produzione video su PC/Hermes: brief, script, storyboard, scene, asset, voce, musica, rischi, prossimi step. Tutti i file finali devono essere pensati per comparire nel feed Video tramite quella cartella monitorata. Se utile, indica stream_url/download_url.\n\nRichiesta utente:\n{prompt}"
            : $"Destinazione: Hermes Hub / News.\nMemoria: usa la memoria agente condivisa Hermes/CLI/app per interessi, fonti preferite, profondita, tono e filtri di qualita. Se impari una preferenza stabile, salvala lato Hermes se possibile.\nObiettivo: crea output operativo per articolo/briefing: query, fonti consultate, filtri, sintesi, frequenza, formato briefing, rischi di affidabilita.\n\nRichiesta utente:\n{prompt}";

        var payload = JsonSerializer.Serialize(new
        {
            model = settings.Model,
            input = runPrompt,
            metadata = new
            {
                client = "hermes-hub",
                client_surface = "windows-app",
                workspace = kind.ToLowerInvariant(),
                source = "workspace-section",
                memory_scope = "shared-hermes-agent-memory",
                share_with_cli = true
            }
        });

        try
        {
            var runResult = await SendHermesRequestAsync(settings, HttpMethod.Post, "/v1/runs", payload);
            if (!runResult.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase))
            {
                return new WorkspaceRunResult(runResult, "Hermes Runs", "Run Hermes completata.");
            }
        }
        catch
        {
            // Fall through to chat fallback.
        }

        var history = new List<ChatMessageRecord>
        {
            new("Tu", runPrompt, DateTimeOffset.Now)
        };
        var chat = await SendChatAsync(settings, "Agente", runPrompt, history);
        return new WorkspaceRunResult(chat.Message, chat.Source, chat.StatusMessage);
    }

    public static async Task<ServerSnapshot> GetServerSnapshotAsync(AppSettings settings)
    {
        var root = HermesRoot(settings);
        var healthUrl = $"{root}/health";

        try
        {
            var healthResponse = await SendBufferedAsync(token => BuildRequest(HttpMethod.Get, healthUrl, token));
            var healthStatus = healthResponse.IsSuccessStatusCode
                ? "Hermes raggiungibile."
                : $"Hermes risponde: HTTP {healthResponse.StatusCode} {healthResponse.ReasonPhrase}";

            try
            {
                var statusResponse = await SendBufferedAsync(token => BuildRequest(HttpMethod.Get, $"{root}/health/detailed", token));
                var statusBody = statusResponse.Body;

                if (statusResponse.IsSuccessStatusCode && TryParseServerSnapshot(statusBody, settings, healthStatus, out var snapshot))
                {
                    return snapshot;
                }
            }
            catch
            {
                // Keep health result and fall back to local settings snapshot.
            }

            return new ServerSnapshot(
                settings.GatewayUrl,
                settings.Model,
                $"Provider: {settings.Provider} | API: {settings.PreferredApi}",
                settings.InferenceEndpoint,
                settings.AccessMode,
                settings.DemoMode
                    ? $"{healthStatus} Fallback locale attivo."
                    : $"{healthStatus} Solo Hermes, nessun fallback locale.",
                settings.VideoLibraryPath);
        }
        catch (Exception ex)
        {
            var modeText = settings.DemoMode
                ? "Fallback locale attivo."
                : "Solo Hermes, nessun fallback locale.";
            return new ServerSnapshot(
                settings.GatewayUrl,
                settings.Model,
                $"Provider: {settings.Provider} | API: {settings.PreferredApi}",
                settings.InferenceEndpoint,
                settings.AccessMode,
                $"Hermes non raggiungibile: {ex.Message}. {modeText}",
                settings.VideoLibraryPath);
        }
    }

    private static GatewayTaskResult BuildTaskFallback(AppSettings settings, AgentTaskRecord task, string message)
    {
        if (settings.DemoMode)
        {
            var fallbackTask = task with
            {
                Status = task.RequiresApproval ? "In attesa approvazione" : "Pronto",
                Source = "Fallback locale",
                Mode = "Locale",
                UpdatedAt = DateTimeOffset.Now
            };
            return new GatewayTaskResult(fallbackTask, $"{message} Salvo il task in locale.");
        }

        var failedTask = task with
        {
            Status = "Errore Hermes",
            Source = "Errore Hermes",
            UpdatedAt = DateTimeOffset.Now
        };
        return new GatewayTaskResult(failedTask, message);
    }

    private static bool TryParseServerSnapshot(
        string body,
        AppSettings settings,
        string healthStatus,
        out ServerSnapshot snapshot)
    {
        snapshot = default!;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var model = ExtractString(root, "model")
                ?? ExtractNestedString(root, "server", "model")
                ?? ExtractNestedString(root, "runtime", "model")
                ?? settings.Model;
            var provider = ExtractString(root, "provider")
                ?? ExtractNestedString(root, "server", "provider")
                ?? settings.Provider;
            var api = ExtractString(root, "preferredApi")
                ?? ExtractString(root, "api")
                ?? ExtractNestedString(root, "server", "api")
                ?? settings.PreferredApi;
            var inference = ExtractString(root, "inferenceEndpoint")
                ?? ExtractNestedString(root, "server", "inferenceEndpoint")
                ?? ExtractNestedString(root, "runtime", "endpoint")
                ?? settings.InferenceEndpoint;
            var policy = ExtractString(root, "accessMode")
                ?? ExtractString(root, "networkPolicy")
                ?? ExtractNestedString(root, "security", "networkPolicy")
                ?? settings.AccessMode;
            var videoLibraryPath = ExtractString(root, "video_library_path")
                ?? ExtractString(root, "videoLibraryPath")
                ?? ExtractNestedString(root, "video", "library_path")
                ?? ExtractNestedString(root, "video", "video_library_path")
                ?? ExtractNestedString(root, "config", "video_library_path")
                ?? settings.VideoLibraryPath;
            var statusMessage = ExtractString(root, "status")
                ?? ExtractString(root, "message")
                ?? healthStatus;

            if (!string.Equals(videoLibraryPath, settings.VideoLibraryPath, StringComparison.Ordinal))
            {
                settings.VideoLibraryPath = videoLibraryPath;
                AppSettingsStore.Save(settings);
            }

            snapshot = new ServerSnapshot(
                settings.GatewayUrl,
                model,
                $"Provider: {provider} | API: {api}",
                inference,
                policy,
                statusMessage,
                settings.VideoLibraryPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildFallbackReply(AppSettings settings, string mode, string? reason)
    {
        var prefix = string.Equals(mode, "Agente", StringComparison.OrdinalIgnoreCase)
            ? "Hermes assente: preparo un job locale e tengo il contesto pronto."
            : "Hermes assente: uso la risposta locale di emergenza senza perdere la conversazione.";

        var detail = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Motivo: {reason}.";
        return $"{prefix} Preset: API {settings.GatewayUrl}, modello {settings.Model}, protocollo {settings.PreferredApi}.{detail}";
    }

    public static async Task<bool> SupportsResponsesAsync(AppSettings settings)
    {
        try
        {
            var response = await SendBufferedAsync(token => BuildRequest(HttpMethod.Get, $"{settings.GatewayUrl.TrimEnd('/')}/capabilities", token));
            var body = response.Body;
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
            {
                return true;
            }

            return body.Contains("responses", StringComparison.OrdinalIgnoreCase) ||
                   body.Contains("Responses", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    public static async Task<string> SendHermesRequestAsync(AppSettings settings, HttpMethod method, string path, string? jsonPayload = null)
    {
        var uri = ResolveHermesUri(settings, path);
        var response = await SendBufferedAsync(token =>
            string.IsNullOrWhiteSpace(jsonPayload)
                ? BuildRequest(method, uri, token)
                : BuildJsonRequest(method, uri, jsonPayload, token));
        var body = response.Body;
        if (response.IsSuccessStatusCode)
        {
            return string.IsNullOrWhiteSpace(body) ? $"HTTP {response.StatusCode} {response.ReasonPhrase}" : body;
        }

        return $"HTTP {response.StatusCode} {response.ReasonPhrase}: {ExtractHumanError(body)}";
    }

    public static string HermesRoot(AppSettings settings)
    {
        var api = settings.GatewayUrl.TrimEnd('/');
        return api.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? api[..^3]
            : api;
    }

    private static string ResolveHermesUri(AppSettings settings, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            return path;
        }

        var normalizedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";
        if (normalizedPath.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.Equals("/v1", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            return $"{HermesRoot(settings)}{normalizedPath}";
        }

        return $"{settings.GatewayUrl.TrimEnd('/')}{normalizedPath}";
    }

    internal static IEnumerable<string?> BuildHermesAuthCandidates()
    {
        yield return null;
        yield return HermesFallbackApiKey;
    }

    internal static bool ShouldRetryWithBearerAuth(int statusCode, string body)
    {
        if (statusCode != 401)
        {
            return false;
        }

        return body.Contains("invalid api key", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("invalidapikey", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<BufferedHermesResponse> SendBufferedAsync(
        Func<string?, HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken = default)
    {
        BufferedHermesResponse? last = null;
        var candidates = BuildHermesAuthCandidates().ToArray();
        for (var i = 0; i < candidates.Length; i++)
        {
            using var request = requestFactory(candidates[i]);
            using var response = await HttpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var buffered = new BufferedHermesResponse(
                (int)response.StatusCode,
                response.ReasonPhrase,
                body,
                response.Content.Headers.ContentType?.MediaType);
            last = buffered;
            if (!ShouldRetryWithBearerAuth(buffered.StatusCode, buffered.Body) || i == candidates.Length - 1)
            {
                return buffered;
            }
        }

        return last ?? new BufferedHermesResponse(0, "No Response", string.Empty, null);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string uri, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "HermesHub-Windows");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken}");
        }

        return request;
    }

    private static HttpRequestMessage BuildJsonRequest(HttpMethod method, string uri, string payload, string? bearerToken = null)
    {
        var request = BuildRequest(method, uri, bearerToken);
        if (method != HttpMethod.Get && method != HttpMethod.Delete)
        {
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static string ExtractAssistantText(string? mediaType, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        if ((mediaType?.Contains("event-stream", StringComparison.OrdinalIgnoreCase) ?? false) ||
            body.Contains("data:", StringComparison.OrdinalIgnoreCase))
        {
            var sseText = ParseSseText(body);
            if (!string.IsNullOrWhiteSpace(sseText))
            {
                return sseText;
            }
        }

        var trimmed = body.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) && !trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return trimmed;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            return ExtractTextFromJson(document.RootElement);
        }
        catch
        {
            return trimmed;
        }
    }

    private static string ParseSseText(string body)
    {
        var builder = new StringBuilder();
        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line[5..].Trim();
            if (string.IsNullOrWhiteSpace(payload) || payload == "[DONE]")
            {
                continue;
            }

            if (payload.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    using var document = JsonDocument.Parse(payload);
                    var piece = ExtractTextFromJson(document.RootElement);
                    if (!string.IsNullOrWhiteSpace(piece))
                    {
                        builder.Append(piece);
                    }
                }
                catch
                {
                    builder.Append(payload);
                }
            }
            else
            {
                builder.Append(payload);
            }
        }

        return builder.ToString().Trim();
    }

    private static string ExtractTextFromJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() ?? string.Empty;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "output_text", "text", "content", "message", "reply" })
            {
                if (element.TryGetProperty(key, out var property))
                {
                    var nested = ExtractTextFromJson(property);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
            }

            foreach (var key in new[] { "delta", "choice", "data" })
            {
                if (element.TryGetProperty(key, out var property))
                {
                    var nested = ExtractTextFromJson(property);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
            }

            if (element.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                foreach (var choice in choices.EnumerateArray())
                {
                    var nested = ExtractTextFromJson(choice);
                    if (!string.IsNullOrWhiteSpace(nested))
                    {
                        return nested;
                    }
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var item in element.EnumerateArray())
            {
                var nested = ExtractTextFromJson(item);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    builder.Append(nested);
                }
            }

            return builder.ToString().Trim();
        }

        return string.Empty;
    }

    private static string? ExtractTaskId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return ExtractString(document.RootElement, "id", "taskId", "job_id", "jobId")
                ?? ExtractNestedString(document.RootElement, "task", "id")
                ?? ExtractNestedString(document.RootElement, "job", "id");
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractTaskStatus(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return ExtractString(document.RootElement, "status")
                ?? ExtractNestedString(document.RootElement, "task", "status")
                ?? ExtractNestedString(document.RootElement, "job", "status");
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractResponseId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return ExtractString(document.RootElement, "id", "response_id", "responseId")
                ?? ExtractNestedString(document.RootElement, "response", "id");
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractHumanError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return ExtractString(document.RootElement, "error", "message", "detail")
                ?? body.Trim();
        }
        catch
        {
            return body.Trim();
        }
    }

    private static string? ExtractNestedString(JsonElement root, string parentKey, string childKey)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(parentKey, out var parent) &&
            parent.ValueKind == JsonValueKind.Object)
        {
            return ExtractString(parent, childKey);
        }

        return null;
    }

    private static string? ExtractString(JsonElement root, params string[] keys)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (!root.TryGetProperty(key, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            var nested = ExtractTextFromJson(property);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        return null;
    }
}
