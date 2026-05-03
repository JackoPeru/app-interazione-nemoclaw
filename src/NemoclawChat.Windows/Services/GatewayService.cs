using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed record GatewayChatResult(string Message, string Source, string StatusMessage, bool UsedFallback);

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
        IReadOnlyList<ChatMessageRecord> history)
    {
        var payload = JsonSerializer.Serialize(new
        {
            mode = mode.ToLowerInvariant(),
            prompt,
            message = prompt,
            model = settings.Model,
            provider = settings.Provider,
            preferredApi = settings.PreferredApi,
            accessMode = settings.AccessMode,
            messages = history.Select(message => new
            {
                role = string.Equals(message.Author, "Tu", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant",
                content = message.Text
            })
        });

        var endpoints = new[]
        {
            $"{settings.GatewayUrl.TrimEnd('/')}/api/chat/stream",
            $"{settings.GatewayUrl.TrimEnd('/')}/api/chat"
        };

        string? lastError = null;

        foreach (var endpoint in endpoints)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                request.Headers.Accept.ParseAdd("text/event-stream");
                request.Headers.Accept.ParseAdd("application/json");
                request.Headers.Accept.ParseAdd("text/plain");
                request.Headers.UserAgent.ParseAdd("ChatClaw-Windows");

                using var response = await HttpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        lastError += $": {ExtractHumanError(body)}";
                    }
                    continue;
                }

                var assistantText = ExtractAssistantText(response.Content.Headers.ContentType?.MediaType, body);
                if (!string.IsNullOrWhiteSpace(assistantText))
                {
                    return new GatewayChatResult(
                        assistantText,
                        "Gateway",
                        "Risposta ricevuta dal gateway.",
                        false);
                }

                lastError = "Gateway raggiunto, ma senza contenuto utile.";
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        if (settings.DemoMode)
        {
            return new GatewayChatResult(
                BuildFallbackReply(settings, mode, lastError),
                "Fallback locale",
                $"Gateway non disponibile, uso fallback locale: {lastError ?? "errore sconosciuto"}.",
                true);
        }

        return new GatewayChatResult(
            $"Gateway non raggiungibile: {lastError ?? "errore sconosciuto"}.",
            "Errore gateway",
            $"Invio fallito: {lastError ?? "errore sconosciuto"}.",
            false);
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
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.GatewayUrl.TrimEnd('/')}/api/tasks")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("ChatClaw-Windows");

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
                    Source = "Gateway",
                    Mode = "Gateway",
                    UpdatedAt = DateTimeOffset.Now
                };
                return new GatewayTaskResult(syncedTask, "Task creato sul gateway.");
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
            TaskCommand.Approve => "Approvato",
            TaskCommand.Deny => "Negato",
            _ => "Completato"
        };

        if (string.IsNullOrWhiteSpace(task.RemoteId))
        {
            var localTask = task with
            {
                Status = $"{targetStatus} locale",
                Source = "Fallback locale",
                UpdatedAt = DateTimeOffset.Now
            };
            return new GatewayTaskResult(localTask, $"Task aggiornato in locale: {targetStatus}.");
        }

        var actionSegment = command switch
        {
            TaskCommand.Approve => "approve",
            TaskCommand.Deny => "deny",
            _ => "complete"
        };

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{settings.GatewayUrl.TrimEnd('/')}/api/tasks/{task.RemoteId}/{actionSegment}")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("ChatClaw-Windows");

            using var response = await HttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var remoteStatus = ExtractTaskStatus(body) ?? targetStatus;
                var syncedTask = task with
                {
                    Status = remoteStatus,
                    Source = "Gateway",
                    UpdatedAt = DateTimeOffset.Now
                };
                return new GatewayTaskResult(syncedTask, $"Task aggiornato sul gateway: {remoteStatus}.");
            }

            var error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            if (!string.IsNullOrWhiteSpace(body))
            {
                error += $": {ExtractHumanError(body)}";
            }

            return BuildTaskFallback(settings, task with { Status = targetStatus }, $"Aggiornamento task fallito: {error}");
        }
        catch (Exception ex)
        {
            return BuildTaskFallback(settings, task with { Status = targetStatus }, $"Aggiornamento task fallito: {ex.Message}");
        }
    }

    public static async Task<ServerSnapshot> GetServerSnapshotAsync(AppSettings settings)
    {
        var healthUrl = $"{settings.GatewayUrl.TrimEnd('/')}/api/health";

        try
        {
            using var healthRequest = new HttpRequestMessage(HttpMethod.Get, healthUrl);
            healthRequest.Headers.UserAgent.ParseAdd("ChatClaw-Windows");
            using var healthResponse = await HttpClient.SendAsync(healthRequest);
            var healthStatus = healthResponse.IsSuccessStatusCode
                ? "Gateway raggiungibile."
                : $"Gateway risponde: HTTP {(int)healthResponse.StatusCode} {healthResponse.ReasonPhrase}";

            try
            {
                using var statusRequest = new HttpRequestMessage(HttpMethod.Get, $"{settings.GatewayUrl.TrimEnd('/')}/api/server/status");
                statusRequest.Headers.Accept.ParseAdd("application/json");
                statusRequest.Headers.UserAgent.ParseAdd("ChatClaw-Windows");

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
                    : $"{healthStatus} Solo gateway, nessun fallback locale.");
        }
        catch (Exception ex)
        {
            var modeText = settings.DemoMode
                ? "Fallback locale attivo."
                : "Solo gateway, nessun fallback locale.";
            return new ServerSnapshot(
                settings.GatewayUrl,
                settings.Model,
                $"Provider: {settings.Provider} | API: {settings.PreferredApi}",
                settings.InferenceEndpoint,
                settings.AccessMode,
                $"Gateway non raggiungibile: {ex.Message}. {modeText}");
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
            Status = "Errore gateway",
            Source = "Errore gateway",
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
            ? "Gateway assente: preparo un ordine locale con approve/deny e tengo il contesto pronto."
            : "Gateway assente: uso la risposta locale di emergenza senza perdere la conversazione.";

        var detail = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Motivo: {reason}.";
        return $"{prefix} Preset: gateway {settings.GatewayUrl}, endpoint server {settings.InferenceEndpoint}, API {settings.PreferredApi}.{detail}";
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
            foreach (var key in new[] { "text", "content", "message", "reply", "output_text" })
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
            return ExtractString(document.RootElement, "id", "taskId")
                ?? ExtractNestedString(document.RootElement, "task", "id");
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
                ?? ExtractNestedString(document.RootElement, "task", "status");
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
