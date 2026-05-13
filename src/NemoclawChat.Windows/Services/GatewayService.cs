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
    string StatusMessage);

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
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

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
                    instructions = mode.Equals("Agente", StringComparison.OrdinalIgnoreCase)
                        ? "Agisci come Hermes Agent operativo. Usa strumenti e memoria disponibili lato server e conserva un riepilogo chiaro delle azioni."
                        : "Rispondi come assistente conversazionale Hermes.",
                    store = true,
                    conversation = string.IsNullOrWhiteSpace(conversationId) ? null : conversationId,
                    previous_response_id = string.IsNullOrWhiteSpace(previousResponseId) ? null : previousResponseId,
                    metadata = new
                    {
                        client = "hermes-hub",
                        visual_blocks = new
                        {
                            min_supported_version = VisualBlocksContract.Version,
                            max_supported_version = VisualBlocksContract.Version,
                            mode = settings.VisualBlocksMode
                        }
                    }
                });

                using var request = BuildJsonRequest(HttpMethod.Post, $"{settings.GatewayUrl.TrimEnd('/')}/responses", responsePayload);
                using var response = await HttpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        lastError += $": {ExtractHumanError(body)}";
                    }
                    lastError = $"Responses API {lastError}";
                }
                else
                {
                    var assistantText = ExtractAssistantText(response.Content.Headers.ContentType?.MediaType, body);
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
                metadata = new
                {
                    client = "hermes-hub",
                    visual_blocks = new
                    {
                        min_supported_version = VisualBlocksContract.Version,
                        max_supported_version = VisualBlocksContract.Version,
                        mode = settings.VisualBlocksMode
                    }
                },
                messages = history.Select(message => new
                {
                    role = string.Equals(message.Author, "Tu", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant",
                    content = message.Text
                })
            });

            using var request = BuildJsonRequest(HttpMethod.Post, $"{settings.GatewayUrl.TrimEnd('/')}/chat/completions", chatPayload);
            using var response = await HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var assistantText = ExtractAssistantText(response.Content.Headers.ContentType?.MediaType, body);
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
                lastError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {ExtractHumanError(body)}";
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
            provider = settings.Provider
        });

        try
        {
            using var request = BuildJsonRequest(HttpMethod.Post, $"{HermesRoot(settings)}/api/jobs", payload);

            using var response = await HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

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

            var error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
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
            using var request = BuildJsonRequest(method, requestUri, "{}");

            using var response = await HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

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

            var error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
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
            ? $"Sezione Video Hermes Hub. Crea output operativo per produzione video: brief, script, storyboard, scene, asset, voce, musica, rischi, prossimi step.\n\nRichiesta utente:\n{prompt}"
            : $"Sezione News Hermes Hub. Crea output operativo per ricerca e aggiornamento: query, fonti da consultare, filtri, sintesi attesa, frequenza, formato briefing, rischi di affidabilita.\n\nRichiesta utente:\n{prompt}";

        var payload = JsonSerializer.Serialize(new
        {
            model = settings.Model,
            input = runPrompt,
            metadata = new
            {
                client = "hermes-hub",
                workspace = kind.ToLowerInvariant(),
                source = "workspace-section"
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
            using var healthRequest = BuildRequest(HttpMethod.Get, healthUrl);
            using var healthResponse = await HttpClient.SendAsync(healthRequest);
            var healthStatus = healthResponse.IsSuccessStatusCode
                ? "Hermes raggiungibile."
                : $"Hermes risponde: HTTP {(int)healthResponse.StatusCode} {healthResponse.ReasonPhrase}";

            try
            {
                using var statusRequest = BuildRequest(HttpMethod.Get, $"{root}/health/detailed");

                using var statusResponse = await HttpClient.SendAsync(statusRequest);
                var statusBody = await statusResponse.Content.ReadAsStringAsync();

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
                    : $"{healthStatus} Solo Hermes, nessun fallback locale.");
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
                $"Hermes non raggiungibile: {ex.Message}. {modeText}");
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
            var statusMessage = ExtractString(root, "status")
                ?? ExtractString(root, "message")
                ?? healthStatus;

            snapshot = new ServerSnapshot(
                settings.GatewayUrl,
                model,
                $"Provider: {provider} | API: {api}",
                inference,
                policy,
                statusMessage);
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
            using var request = BuildRequest(HttpMethod.Get, $"{settings.GatewayUrl.TrimEnd('/')}/capabilities");
            using var response = await HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
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
        using var request = string.IsNullOrWhiteSpace(jsonPayload)
            ? BuildRequest(method, uri)
            : BuildJsonRequest(method, uri, jsonPayload);
        using var response = await HttpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            return string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}" : body;
        }

        return $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {ExtractHumanError(body)}";
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

    private static HttpRequestMessage BuildRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "HermesHub-Windows");
        var apiKey = GatewayCredentialStore.LoadSecret();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        return request;
    }

    private static HttpRequestMessage BuildJsonRequest(HttpMethod method, string uri, string payload)
    {
        var request = BuildRequest(method, uri);
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
