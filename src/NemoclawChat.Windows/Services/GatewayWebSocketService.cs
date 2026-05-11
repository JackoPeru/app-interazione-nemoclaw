using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed record GatewayWebSocketProbe(
    string WsUrl,
    bool Connected,
    string Status,
    string Details,
    IReadOnlyList<string> CapabilityLines);

public sealed record GatewayRpcCallResult(
    string WsUrl,
    string Method,
    bool Success,
    string Status,
    string RawJson,
    string Summary);

public static class GatewayWebSocketService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string NormalizeWebSocketUrl(string wsUrl, string gatewayUrl)
    {
        var candidate = string.IsNullOrWhiteSpace(wsUrl) ? gatewayUrl : wsUrl;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return "wss://openclaw.local:8443";
        }

        var builder = new UriBuilder(uri);
        builder.Scheme = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            ? "wss"
            : uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                ? "ws"
                : uri.Scheme;
        return builder.Uri.ToString().TrimEnd('/');
    }

    public static async Task<GatewayWebSocketProbe> ProbeAsync(AppSettings settings, string? authSecret, CancellationToken cancellationToken = default)
    {
        var wsUrl = NormalizeWebSocketUrl(settings.GatewayWsUrl, settings.GatewayUrl);
        if (!Uri.TryCreate(wsUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            return new GatewayWebSocketProbe(wsUrl, false, "URL WebSocket non valido.", "Usa ws:// o wss://.", Array.Empty<string>());
        }

        try
        {
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("User-Agent", "ChatClaw-Windows");
            if (!string.IsNullOrWhiteSpace(authSecret))
            {
                socket.Options.SetRequestHeader("Authorization", $"Bearer {authSecret.Trim()}");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));

            await socket.ConnectAsync(uri, timeout.Token);
            await SendJsonAsync(socket, BuildConnectFrame(authSecret), timeout.Token);
            var hello = await ReceiveJsonAsync(socket, timeout.Token);

            var lines = new List<string>();
            foreach (var method in new[]
                     {
                         "status",
                         "system-presence",
                         "models.status",
                         "models.list",
                         "plugins.list",
                         "channels.status",
                         "nodes.list",
                         "exec.approvals.get"
                     })
            {
                var result = await TryCallAsync(socket, method, timeout.Token);
                lines.Add(result);
            }

            return new GatewayWebSocketProbe(
                wsUrl,
                true,
                "Gateway WS legacy connesso. Handshake riuscito.",
                SummarizeFrame(hello),
                lines);
        }
        catch (OperationCanceledException)
        {
            return new GatewayWebSocketProbe(wsUrl, false, "Gateway WS timeout.", "Nessuna risposta entro 8 secondi.", Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return new GatewayWebSocketProbe(wsUrl, false, "Gateway WS non raggiungibile.", ex.Message, Array.Empty<string>());
        }
    }

    public static async Task<GatewayRpcCallResult> CallAsync(
        AppSettings settings,
        string? authSecret,
        string method,
        string rawParams,
        CancellationToken cancellationToken = default)
    {
        var wsUrl = NormalizeWebSocketUrl(settings.GatewayWsUrl, settings.GatewayUrl);
        if (string.IsNullOrWhiteSpace(method))
        {
            return new GatewayRpcCallResult(wsUrl, method, false, "Metodo RPC obbligatorio.", string.Empty, string.Empty);
        }

        if (!TryBuildParams(rawParams, out var parameters, out var paramsError))
        {
            return new GatewayRpcCallResult(wsUrl, method, false, paramsError, string.Empty, string.Empty);
        }

        if (!Uri.TryCreate(wsUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            return new GatewayRpcCallResult(wsUrl, method, false, "URL WebSocket non valido.", string.Empty, "Usa ws:// o wss://.");
        }

        try
        {
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("User-Agent", "ChatClaw-Windows");
            if (!string.IsNullOrWhiteSpace(authSecret))
            {
                socket.Options.SetRequestHeader("Authorization", $"Bearer {authSecret.Trim()}");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));

            await socket.ConnectAsync(uri, timeout.Token);
            await SendJsonAsync(socket, BuildConnectFrame(authSecret), timeout.Token);
            _ = await ReceiveJsonAsync(socket, timeout.Token);

            var id = NewId();
            await SendJsonAsync(socket, new
            {
                type = "req",
                id,
                method = method.Trim(),
                @params = parameters
            }, timeout.Token);

            using var response = await ReceiveMatchingJsonAsync(socket, id, timeout.Token);
            var raw = response.RootElement.GetRawText();
            var hasError = response.RootElement.TryGetProperty("error", out _);
            return new GatewayRpcCallResult(
                wsUrl,
                method.Trim(),
                !hasError,
                hasError ? "RPC errore." : "RPC completata.",
                PrettyJson(raw),
                SummarizeFrame(response));
        }
        catch (OperationCanceledException)
        {
            return new GatewayRpcCallResult(wsUrl, method, false, "RPC timeout.", string.Empty, "Nessuna risposta entro 12 secondi.");
        }
        catch (Exception ex)
        {
            return new GatewayRpcCallResult(wsUrl, method, false, "RPC fallita.", string.Empty, ex.Message);
        }
    }

    private static object BuildConnectFrame(string? authSecret)
    {
        return new
        {
            type = "req",
            id = NewId(),
            method = "connect",
            @params = new
            {
                minProtocol = 3,
                maxProtocol = 3,
                client = new
                {
                    id = "chatclaw-windows",
                    version = "0.5.4",
                    platform = "windows",
                    mode = "operator"
                },
                role = "operator",
                scopes = new[]
                {
                    "operator.read",
                    "operator.write",
                    "operator.approvals",
                    "operator.pairing"
                },
                caps = Array.Empty<string>(),
                commands = Array.Empty<string>(),
                permissions = new { },
                auth = string.IsNullOrWhiteSpace(authSecret) ? null : new { token = authSecret.Trim() },
                locale = "it-IT",
                userAgent = "ChatClaw-Windows/0.5.4",
                device = new
                {
                    id = BuildDeviceId(),
                    signedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            }
        };
    }

    private static async Task<string> TryCallAsync(ClientWebSocket socket, string method, CancellationToken cancellationToken)
    {
        try
        {
            await SendJsonAsync(socket, new
            {
                type = "req",
                id = NewId(),
                method,
                @params = new { }
            }, cancellationToken);

            var response = await ReceiveJsonAsync(socket, cancellationToken);
            return $"{method}: {SummarizeFrame(response)}";
        }
        catch (Exception ex)
        {
            return $"{method}: non disponibile ({ex.Message})";
        }
    }

    private static async Task SendJsonAsync(ClientWebSocket socket, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("Socket chiuso dal gateway.");
            }

            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        var body = Encoding.UTF8.GetString(stream.ToArray());
        return JsonDocument.Parse(body);
    }

    private static async Task<JsonDocument> ReceiveMatchingJsonAsync(ClientWebSocket socket, string id, CancellationToken cancellationToken)
    {
        while (true)
        {
            var document = await ReceiveJsonAsync(socket, cancellationToken);
            if (!document.RootElement.TryGetProperty("id", out var responseId) ||
                responseId.ValueKind != JsonValueKind.String ||
                !string.Equals(responseId.GetString(), id, StringComparison.Ordinal))
            {
                document.Dispose();
                continue;
            }

            return document;
        }
    }

    private static bool TryBuildParams(string rawParams, out JsonElement parameters, out string error)
    {
        var raw = string.IsNullOrWhiteSpace(rawParams) ? "{}" : rawParams.Trim();
        try
        {
            using var document = JsonDocument.Parse(raw);
            parameters = document.RootElement.Clone();
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            parameters = default;
            error = $"Parametri JSON non validi: {ex.Message}";
            return false;
        }
    }

    private static string PrettyJson(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return raw;
        }
    }

    private static string SummarizeFrame(JsonDocument document)
    {
        var root = document.RootElement;
        if (root.TryGetProperty("error", out var error))
        {
            return $"errore: {ExtractText(error)}";
        }

        foreach (var key in new[] { "result", "data", "payload", "params" })
        {
            if (root.TryGetProperty(key, out var value))
            {
                var text = ExtractText(value);
                return string.IsNullOrWhiteSpace(text) ? value.GetRawText().Truncate(180) : text.Truncate(180);
            }
        }

        return root.GetRawText().Truncate(180);
    }

    private static string ExtractText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() ?? string.Empty;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "message", "status", "version", "name", "id" })
            {
                if (element.TryGetProperty(key, out var property))
                {
                    var text = ExtractText(property);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        return string.Empty;
    }

    private static string BuildDeviceId()
    {
        var source = $"chatclaw-windows:{Environment.MachineName}:{Environment.UserName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return $"chatclaw-win-{Convert.ToHexString(hash[..6]).ToLowerInvariant()}";
    }

    private static string NewId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string Truncate(this string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
