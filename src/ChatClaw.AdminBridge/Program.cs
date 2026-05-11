using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();
var config = BridgeConfig.Load();
var audit = new AuditLog(config.AuditPath);

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/v1/health")
    {
        await next();
        return;
    }

    var token = context.Request.Headers.Authorization.ToString();
    var expected = config.Token;
    if (string.IsNullOrWhiteSpace(expected) ||
        !token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
        !CryptographicEquals(token["Bearer ".Length..].Trim(), expected))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { status = "unauthorized", message = "Bearer token obbligatorio." });
        return;
    }

    await next();
});

app.MapGet("/v1/health", () => Results.Json(new { status = "ok", app = "ChatClaw Admin Bridge", version = "0.1.0" }));

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

    audit.Write("logs.tail", path);
    var lines = await File.ReadAllLinesAsync(path);
    return Results.Json(new
    {
        status = "ok",
        path,
        lines = lines.TakeLast(Math.Clamp(request.Lines <= 0 ? 200 : request.Lines, 1, 2000))
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

    var backupPath = File.Exists(path) ? $"{path}.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.bak" : null;
    if (backupPath is not null)
    {
        File.Copy(path, backupPath);
    }

    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await File.WriteAllTextAsync(path, request.Text ?? string.Empty);
    audit.Write("files.write", path);
    return Results.Json(new { status = "ok", path, backupPath });
});

app.Run();

static bool CryptographicEquals(string left, string right)
{
    var leftBytes = System.Text.Encoding.UTF8.GetBytes(left);
    var rightBytes = System.Text.Encoding.UTF8.GetBytes(right);
    return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
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
            CommandTimeoutSeconds = int.TryParse(Environment.GetEnvironmentVariable("CHATCLAW_ADMIN_TIMEOUT"), out var timeout) ? timeout : 120,
            MaxReadBytes = long.TryParse(Environment.GetEnvironmentVariable("CHATCLAW_ADMIN_MAX_READ_BYTES"), out var maxRead) ? maxRead : 1_000_000,
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

        var full = Path.GetFullPath(requested);
        return Roots.Any(root => full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            ? full
            : null;
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
    public void Write(string action, string detail)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, JsonSerializer.Serialize(new
        {
            at = DateTimeOffset.UtcNow,
            action,
            detail
        }) + Environment.NewLine);
    }
}

static class CommandRunner
{
    public static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(string fileName, string arguments, int timeoutSeconds)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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
            process.Kill(true);
            return (-1, await stdoutTask, "Timeout: processo terminato.");
        }

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }
}
