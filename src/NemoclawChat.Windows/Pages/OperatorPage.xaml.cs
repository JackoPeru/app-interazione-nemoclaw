using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class OperatorPage : Page
{
    private readonly IReadOnlyList<OperatorPreset> _presets =
    [
        new("Dashboard", "Health", HttpMethod.Get, "/health", string.Empty),
        new("Dashboard", "Health detailed", HttpMethod.Get, "/health/detailed", string.Empty),
        new("Dashboard", "Capabilities", HttpMethod.Get, "/v1/capabilities", string.Empty),
        new("Modelli", "Lista modelli", HttpMethod.Get, "/v1/models", string.Empty),
        new("Runs", "Crea run", HttpMethod.Post, "/v1/runs", "{\"model\":\"hermes-agent\",\"input\":\"Controlla stato operativo e riassumi.\"}"),
        new("Jobs", "Lista jobs", HttpMethod.Get, "/api/jobs", string.Empty),
        new("Jobs", "Crea job", HttpMethod.Post, "/api/jobs", "{\"title\":\"Controllo operativo\",\"instructions\":\"Controlla stato Hermes e segnala problemi.\"}")
    ];

    public OperatorPage()
    {
        InitializeComponent();
        ParamsBox.Text = string.Empty;
        ConfigPatchBox.Text = "\"Controlla lo stato operativo e riassumi.\"";
        BuildPresetButtons();
    }

    private void BuildPresetButtons()
    {
        foreach (var group in _presets.GroupBy(preset => preset.Group))
        {
            PresetPanel.Children.Add(new TextBlock
            {
                Text = group.Key,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["MutedTextBrush"],
                Margin = new Thickness(0, 8, 12, 8),
                Width = 92,
                VerticalAlignment = VerticalAlignment.Center
            });

            foreach (var preset in group)
            {
                var button = new Button
                {
                    Content = preset.Label,
                    Tag = preset,
                    Margin = new Thickness(0, 4, 8, 4)
                };
                button.Click += Preset_Click;
                PresetPanel.Children.Add(button);
            }
        }
    }

    private async void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: OperatorPreset preset })
        {
            MethodBox.Text = $"{preset.Method.Method} {preset.Path}";
            ParamsBox.Text = preset.Payload;
            await RunHermesAsync(preset.Method, preset.Path, preset.Payload);
        }
    }

    private async void RunRpc_Click(object sender, RoutedEventArgs e)
    {
        await RunRpcAsync();
    }

    private async Task RunRpcAsync()
    {
        var settings = AppSettingsStore.Load();
        var target = MethodBox.Text.Trim();
        var (method, path) = ParseManualTarget(target, ParamsBox.Text);
        await RunHermesAsync(method, path, ParamsBox.Text);
    }

    private async Task RunHermesAsync(HttpMethod method, string path, string? payload)
    {
        var settings = AppSettingsStore.Load();
        StatusText.Text = $"{method.Method} {path} verso Hermes...";
        ResultBox.Text = string.Empty;
        SummaryText.Text = "Attesa risposta Hermes...";

        var result = await GatewayService.SendHermesRequestAsync(
            settings,
            method,
            path,
            string.IsNullOrWhiteSpace(payload) ? null : payload);

        StatusText.Text = $"Completato: {method.Method} {path}";
        SummaryText.Text = "Risposta Hermes ricevuta.";
        ResultBox.Text = result;
    }

    private async void RefreshApprovals_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "GET /api/jobs";
        ParamsBox.Text = string.Empty;
        await RunHermesAsync(HttpMethod.Get, "/api/jobs", null);
    }

    private async void ApproveOnce_Click(object sender, RoutedEventArgs e)
    {
        await ResolveApprovalAsync("allow-once");
    }

    private async void ApproveAlways_Click(object sender, RoutedEventArgs e)
    {
        await ResolveApprovalAsync("allow-always");
    }

    private async void DenyApproval_Click(object sender, RoutedEventArgs e)
    {
        await ResolveApprovalAsync("deny");
    }

    private async Task ResolveApprovalAsync(string decision)
    {
        var path = $$"""/api/jobs/{{JsonEscape(ApprovalIdBox.Text)}}/{{(decision == "deny" ? "pause" : "run")}}""";
        MethodBox.Text = $"POST {path}";
        ParamsBox.Text = "{}";
        await RunHermesAsync(HttpMethod.Post, path, "{}");
    }

    private async void ReadConfig_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "GET /v1/capabilities";
        ParamsBox.Text = string.Empty;
        await RunHermesAsync(HttpMethod.Get, "/v1/capabilities", null);
    }

    private async void PatchConfig_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "POST /v1/runs";
        ParamsBox.Text = $$"""{"model":"hermes-agent","input":{{ConfigPatchBox.Text}}}""";
        await RunHermesAsync(HttpMethod.Post, "/v1/runs", ParamsBox.Text);
    }

    private async void ApplyConfig_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "GET /v1/models";
        ParamsBox.Text = string.Empty;
        await RunHermesAsync(HttpMethod.Get, "/v1/models", null);
    }

    private async void WorkspaceList_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "GET /api/jobs";
        ParamsBox.Text = string.Empty;
        await RunHermesAsync(HttpMethod.Get, "/api/jobs", null);
    }

    private async void WorkspaceRead_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "GET /health/detailed";
        ParamsBox.Text = string.Empty;
        await RunHermesAsync(HttpMethod.Get, "/health/detailed", null);
    }

    private async void WorkspaceWrite_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "POST /v1/runs";
        ParamsBox.Text = $$"""{"model":"hermes-agent","input":"{{JsonEscape(WorkspaceTextBox.Text)}}"}""";
        await RunHermesAsync(HttpMethod.Post, "/v1/runs", ParamsBox.Text);
    }

    private async void AdminStatus_Click(object sender, RoutedEventArgs e)
    {
        await RunAdminAsync(HttpMethod.Get, "/v1/status");
    }

    private async void AdminDoctor_Click(object sender, RoutedEventArgs e)
    {
        await RunAdminAsync(HttpMethod.Post, "/v1/actions/doctor");
    }

    private async void AdminSecurity_Click(object sender, RoutedEventArgs e)
    {
        await RunAdminAsync(HttpMethod.Post, "/v1/actions/security-audit");
    }

    private async void AdminRestart_Click(object sender, RoutedEventArgs e)
    {
        await RunAdminAsync(HttpMethod.Post, "/v1/actions/restart-gateway");
    }

    private async void AdminTail_Click(object sender, RoutedEventArgs e)
    {
        await RunAdminAsync(HttpMethod.Post, "/v1/logs/tail", new { path = AdminPathBox.Text, lines = 200 });
    }

    private async Task RunAdminAsync(HttpMethod method, string path, object? payload = null)
    {
        var settings = AppSettingsStore.Load();
        var secret = GatewayCredentialStore.LoadSecret();
        StatusText.Text = $"Admin Bridge {path}...";
        SummaryText.Text = "Attesa risposta Admin Bridge...";
        ResultBox.Text = string.Empty;

        var result = await AdminBridgeService.CallAsync(settings, secret, method, path, payload);
        StatusText.Text = result.Status;
        SummaryText.Text = result.Summary;
        ResultBox.Text = string.IsNullOrWhiteSpace(result.RawJson) ? result.Summary : result.RawJson;
    }

    private static string JsonEscape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static (HttpMethod Method, string Path) ParseManualTarget(string target, string payload)
    {
        var parts = target.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[0].Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return (HttpMethod.Get, parts[1]);
        }

        if (parts.Length == 2 && parts[0].Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return (HttpMethod.Delete, parts[1]);
        }

        if (parts.Length == 2 && parts[0].Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            return (HttpMethod.Patch, parts[1]);
        }

        if (parts.Length == 2 && parts[0].Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            return (HttpMethod.Post, parts[1]);
        }

        return (string.IsNullOrWhiteSpace(payload) ? HttpMethod.Get : HttpMethod.Post, target);
    }

    private sealed record OperatorPreset(string Group, string Label, HttpMethod Method, string Path, string Payload);
}
