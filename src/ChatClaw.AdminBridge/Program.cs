using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
    {
        policy.WithOrigins("https://hermes.local", "http://hermes.local")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpCtx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var config = BridgeConfig.Load();
if (string.IsNullOrWhiteSpace(config.Token))
{
    Console.Error.WriteLine("[admin-bridge] FATAL: variabile CHATCLAW_ADMIN_TOKEN non impostata. Impossibile avviare senza auth.");
    return 1;
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = config.MaxRequestBytes;
});

var app = builder.Build();
var audit = new AuditLog(config.AuditPath);
app.UseCors("default");
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/v1/health")
    {
        await next();
        return;
    }

    var token = context.Request.Headers.Authorization.ToString();
    var expected = config.Token;
    const string prefix = "Bearer ";
    var presented = token.Length > prefix.Length && token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? token[prefix.Length..].Trim()
        : string.Empty;
    if (presented.Length == 0 || !CryptographicEquals(presented, expected))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { status = "unauthorized", message = "Bearer token obbligatorio." });
        return;
    }

    await next();
});

app.MapGet("/v1/health", () => Results.Json(new { status = "ok", app = "ChatClaw Admin Bridge", version = "0.1.0" }));

app.MapPost("/v1/reload", () =>
{
    var reloaded = BridgeConfig.Load();
    if (string.IsNullOrWhiteSpace(reloaded.Token))
    {
        audit.Write("config.reload", "denied:missing-token");
        return Results.BadRequest(new { status = "denied", message = "CHATCLAW_ADMIN_TOKEN mancante: config non applicata." });
    }

    config = reloaded;
    audit = new AuditLog(config.AuditPath);
    audit.Write("config.reload", "ok");
    return Results.Json(new { status = "ok", roots = config.Roots });
});

app.MapGet("/v1/status", () =>
{
    audit.Write("status", "read");
    var drives = DriveInfo.GetDrives()
        .Where(drive => drive.IsReady)
        .Select(drive => new
        {
            name = drive.Name,
            totalBytes = drive.TotalSize,
            freeBytes = drive.AvailableFreeSpace
        });

    return Results.Json(new
    {
        status = "ok",
        machine = Environment.MachineName,
        os = RuntimeInformation.OSDescription,
        arch = RuntimeInformation.OSArchitecture.ToString(),
        processUptimeSeconds = (long)(DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
        memory = new
        {
            processBytes = Environment.WorkingSet,
            gcBytes = GC.GetTotalMemory(false)
        },
        roots = config.Roots,
        drives
    });
});

app.MapPost("/v1/actions/{action}", async (string action) =>
{
    var command = config.ResolveAction(action);
    if (command is null)
    {
        audit.Write(action, "denied");
        return Results.BadRequest(new { status = "denied", message = $"Azione non in allowlist: {action}" });
    }

    audit.Write(action, "run");
    var result = await CommandRunner.RunAsync(command.Value.FileName, command.Value.Arguments, config.CommandTimeoutSeconds);
    return Results.Json(new
    {
        status = result.ExitCode == 0 ? "ok" : "error",
        action,
        result.ExitCode,
        result.Stdout,
        result.Stderr
    });
});

app.MapPost("/v1/logs/tail", async (TailRequest request) =>
{
    var path = config.ResolveSafePath(request.Path);
    if (path is null || !File.Exists(path))
    {
        audit.Write("logs.tail", "denied");
        return Results.BadRequest(new { status = "denied", message = "Log non trovato o fuori dalle root consentite." });
    }

    var logInfo = new FileInfo(path);
    if (logInfo.Length > config.MaxReadBytes)
    {
        audit.Write("logs.tail", "denied:size");
        return Results.BadRequest(new { status = "denied", message = $"Log troppo grande. Limite {config.MaxReadBytes} bytes." });
    }

    audit.Write("logs.tail", path);
    var requested = Math.Clamp(request.Lines <= 0 ? 200 : request.Lines, 1, 2000);
    var lines = await ReadLastLinesAsync(path, requested);
    return Results.Json(new
    {
        status = "ok",
        path,
        lines
    });
});

app.MapPost("/v1/files/list", (FileListRequest request) =>
{
    var path = config.ResolveSafePath(request.Path);
    if (path is null || !Directory.Exists(path))
    {
        audit.Write("files.list", "denied");
        return Results.BadRequest(new { status = "denied", message = "Directory non trovata o fuori dalle root consentite." });
    }

    audit.Write("files.list", path);
    var entries = Directory.EnumerateFileSystemEntries(path)
        .Take(500)
        .Select(entry => new
        {
            path = entry,
            name = Path.GetFileName(entry),
            type = Directory.Exists(entry) ? "directory" : "file",
            bytes = File.Exists(entry) ? new FileInfo(entry).Length : 0
        });

    return Results.Json(new { status = "ok", path, entries });
});

app.MapPost("/v1/files/read", async (FileReadRequest request) =>
{
    var path = config.ResolveSafePath(request.Path);
    if (path is null || !File.Exists(path))
    {
        audit.Write("files.read", "denied");
        return Results.BadRequest(new { status = "denied", message = "File non trovato o fuori dalle root consentite." });
    }

    var info = new FileInfo(path);
    if (info.Length > config.MaxReadBytes)
    {
        return Results.BadRequest(new { status = "denied", message = $"File troppo grande. Limite {config.MaxReadBytes} bytes." });
    }

    audit.Write("files.read", path);
    return Results.Json(new { status = "ok", path, text = await File.ReadAllTextAsync(path) });
});

app.MapPost("/v1/files/write", async (FileWriteRequest request) =>
{
    var path = config.ResolveSafePath(request.Path);
    if (path is null)
    {
        audit.Write("files.write", "denied");
        return Results.BadRequest(new { status = "denied", message = "Path fuori dalle root consentite." });
    }

    var payload = request.Text ?? string.Empty;
    if (payload.Length > config.MaxWriteChars)
    {
        audit.Write("files.write", "denied:size");
        return Results.BadRequest(new { status = "denied", message = $"Payload troppo grande. Limite {config.MaxWriteChars} caratteri." });
    }

    var writeLock = PathWriteLocks.Get(path);
    await writeLock.WaitAsync();
    string? backupPath = null;
    try
    {
        if (File.Exists(path))
        {
            backupPath = $"{path}.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.bak";
            try
            {
                File.Copy(path, backupPath);
            }
            catch (IOException ex)
            {
                audit.Write("files.write", $"backup-failed:{ex.GetType().Name}");
                return Results.Problem($"Backup non riuscito: {ex.Message}", statusCode: 503);
            }
            catch (UnauthorizedAccessException ex)
            {
                audit.Write("files.write", $"backup-denied:{ex.GetType().Name}");
                return Results.Problem($"Backup negato: {ex.Message}", statusCode: 503);
            }
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, payload);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            audit.Write("files.write", $"failed:{ex.GetType().Name}");
            return Results.Problem($"Scrittura fallita: {ex.Message}", statusCode: 500);
        }
    }
    finally
    {
        writeLock.Release();
    }

    audit.Write("files.write", path);
    return Results.Json(new { status = "ok", path, backupPath });
});

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("[admin-bridge] shutdown gracefully...");
});

await app.RunAsync();
return 0;

static async Task<IReadOnlyList<string>> ReadLastLinesAsync(string path, int count)
{
    const int chunkSize = 8192;
    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    var length = stream.Length;
    var buffer = new byte[chunkSize];
    var collected = new List<string>(count + 1);
    var tail = new System.Text.StringBuilder();
    long position = length;
    while (position > 0 && collected.Count <= count)
    {
        var read = (int)Math.Min(chunkSize, position);
        position -= read;
        stream.Seek(position, SeekOrigin.Begin);
        var got = await stream.ReadAsync(buffer.AsMemory(0, read));
        if (got <= 0) break;
        var chunk = System.Text.Encoding.UTF8.GetString(buffer, 0, got);
        tail.Insert(0, chunk);
        var text = tail.ToString();
        var nl = text.LastIndexOf('\n');
        while (nl >= 0 && collected.Count <= count)
        {
            var line = text[(nl + 1)..];
            if (line.EndsWith('\r')) line = line[..^1];
            if (line.Length > 0 || collected.Count > 0)
            {
                collected.Insert(0, line);
            }
            text = text[..nl];
            nl = text.LastIndexOf('\n');
        }
        tail.Clear();
        tail.Append(text);
    }
    if (tail.Length > 0 && collected.Count <= count)
    {
        var line = tail.ToString();
        if (line.EndsWith('\r')) line = line[..^1];
        if (line.Length > 0) collected.Insert(0, line);
    }
    if (collected.Count > count)
    {
        collected.RemoveRange(0, collected.Count - count);
    }
    return collected;
}

static bool CryptographicEquals(string left, string right)
{
    var leftBytes = System.Text.Encoding.UTF8.GetBytes(left);
    var rightBytes = System.Text.Encoding.UTF8.GetBytes(right);
    return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
}

static class PathWriteLocks
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _locks =
        new(StringComparer.OrdinalIgnoreCase);

    public static SemaphoreSlim Get(string path) =>
        _locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
}

sealed record TailRequest(string Path, int Lines);
sealed record FileListRequest(string Path);
sealed record FileReadRequest(string Path);
sealed record FileWriteRequest(string Path, string? Text);

sealed class BridgeConfig
{
    public required string Token { get; init; }
    public required string[] Roots { get; init; }
    public required string AuditPath { get; init; }
    public required int CommandTimeoutSeconds { get; init; }
    public required long MaxReadBytes { get; init; }
    public required long MaxRequestBytes { get; init; }
    public required int MaxWriteChars { get; init; }
    public required Dictionary<string, (string FileName, string Arguments)> Actions { get; init; }

    public static BridgeConfig Load()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var data = Path.Combine(home, ".chatclaw-admin-bridge");
        Directory.CreateDirectory(data);

        var roots = (Environment.GetEnvironmentVariable("CHATCLAW_ADMIN_ROOTS") ?? Path.Combine(home, "hermes-workspace"))
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => Path.GetFullPath(path))
            .ToArray();
        foreach (var root in roots)
        {
            Directory.CreateDirectory(root);
        }

        var actions = new Dictionary<string, (string FileName, string Arguments)>(StringComparer.OrdinalIgnoreCase);

        var restart = Environment.GetEnvironmentVariable("CHATCLAW_RESTART_GATEWAY_COMMAND");
        if (!string.IsNullOrWhiteSpace(restart))
        {
            var parts = SplitCommand(restart);
            actions["restart-service"] = (parts.FileName, parts.Arguments);
        }

        return new BridgeConfig
        {
            Token = Environment.GetEnvironmentVariable("CHATCLAW_ADMIN_TOKEN") ?? string.Empty,
            Roots = roots,
            AuditPath = Environment.GetEnvironmentVariable("CHATCLAW_ADMIN_AUDIT") ?? Path.Combine(data, "audit.log"),
            CommandTimeoutSeconds = Math.Max(1, int.TryParse(Environment.GetEnvironmentVariable("CHATCLAW_ADMIN_TIMEOUT"), out var timeout) ? timeout : 120),
            MaxReadBytes = Math.Max(1024, long.TryParse(Environment.GetEnvironmentVariable("CHATCLAW_ADMIN_MAX_READ_BYTES"), out var maxRead) ? maxRead : 1_000_000),
            MaxRequestBytes = Math.Max(1024, long.TryParse(Environment.GetEnvironmentVariable("CHATCLAW_ADMIN_MAX_REQUEST_BYTES"), out var maxReq) ? maxReq : 4_000_000),
            MaxWriteChars = Math.Max(1, int.TryParse(Environment.GetEnvironmentVariable("CHATCLAW_ADMIN_MAX_WRITE_CHARS"), out var maxWrite) ? maxWrite : 2_000_000),
            Actions = actions
        };
    }

    public (string FileName, string Arguments)? ResolveAction(string action)
    {
        return Actions.TryGetValue(action, out var command) ? command : null;
    }

    public string? ResolveSafePath(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            requested = Roots.FirstOrDefault() ?? ".";
        }

        string canonical;
        try
        {
            canonical = Path.GetFullPath(requested);
            // Resolve symlinks / junctions so attacker non puo' creare un junction
            // dentro una root che punta fuori (e.g. C:\Windows\System32).
            if (Directory.Exists(canonical))
            {
                var info = new DirectoryInfo(canonical);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return null;
                }
                var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
                if (resolved is DirectoryInfo dirResolved)
                {
                    canonical = dirResolved.FullName;
                }
            }
            else if (File.Exists(canonical))
            {
                var info = new FileInfo(canonical);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return null;
                }
                var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
                if (resolved is FileInfo fileResolved)
                {
                    canonical = fileResolved.FullName;
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        foreach (var root in Roots)
        {
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (canonical.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                canonical.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return canonical;
            }
        }
        return null;
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        var firstSpace = command.IndexOf(' ');
        return firstSpace < 0
            ? (command, string.Empty)
            : (command[..firstSpace], command[(firstSpace + 1)..]);
    }
}

sealed class AuditLog(string path)
{
    private const long MaxBytes = 10L * 1024 * 1024;
    private const int MaxRotations = 5;
    private readonly object _lock = new();

    public void Write(string action, string detail)
    {
        var line = JsonSerializer.Serialize(new
        {
            at = DateTimeOffset.UtcNow,
            action,
            detail
        }) + Environment.NewLine;

        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                RotateIfNeeded();
                File.AppendAllText(path, line, System.Text.Encoding.UTF8);
            }
            catch (IOException)
            {
                // audit failure non blocca request
            }
            catch (UnauthorizedAccessException)
            {
                // idem
            }
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < MaxBytes) return;
            for (var i = MaxRotations - 1; i >= 1; i--)
            {
                var src = $"{path}.{i}";
                var dst = $"{path}.{i + 1}";
                if (!File.Exists(src)) continue;
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(src, dst);
            }
            File.Move(path, $"{path}.1", overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

static class CommandRunner
{
    public static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(string fileName, string arguments, int timeoutSeconds)
    {
        using var process = new Process();
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in SplitArguments(arguments))
        {
            startInfo.ArgumentList.Add(arg);
        }
        process.StartInfo = startInfo;

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Process gia' uscito: ignora.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Access denied / handle non valido: ignora.
            }
            string stdoutOnTimeout;
            try { stdoutOnTimeout = await stdoutTask; }
            catch { stdoutOnTimeout = string.Empty; }
            return (-1, stdoutOnTimeout, "Timeout: processo terminato.");
        }

        string stdoutFinal;
        string stderrFinal;
        try { stdoutFinal = await stdoutTask; } catch { stdoutFinal = string.Empty; }
        try { stderrFinal = await stderrTask; } catch { stderrFinal = string.Empty; }
        return (process.ExitCode, stdoutFinal, stderrFinal);
    }

    private static IEnumerable<string> SplitArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            yield break;
        }

        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var ch in arguments)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
