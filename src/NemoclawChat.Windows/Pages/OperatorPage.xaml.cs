using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class OperatorPage : Page
{
    private readonly IReadOnlyList<OperatorPreset> _presets =
    [
        new("Dashboard", "Status", "status", "{}"),
        new("Dashboard", "Presenza", "system-presence", "{}"),
        new("Dashboard", "Log recenti", "logs.tail", "{\"sinceMs\":600000,\"limit\":100}"),
        new("Modelli", "Stato modelli", "models.status", "{}"),
        new("Modelli", "Lista modelli", "models.list", "{}"),
        new("Modelli", "Scan provider", "models.scan", "{}"),
        new("Plugin", "Lista plugin", "plugins.list", "{}"),
        new("Plugin", "Doctor plugin", "plugins.doctor", "{}"),
        new("Approvazioni", "Pending exec", "exec.approvals.get", "{}"),
        new("Config", "Leggi config", "config.get", "{}"),
        new("Canali", "Stato canali", "channels.status", "{}"),
        new("Canali", "Lista canali", "channels.list", "{}"),
        new("Cron", "Stato cron", "cron.status", "{}"),
        new("Cron", "Lista cron", "cron.list", "{\"all\":true}"),
        new("Nodi", "Lista nodi", "nodes.list", "{}"),
        new("Security", "Audit sicurezza", "security.audit", "{\"deep\":true}"),
        new("Memoria", "Stato memoria", "memory.status", "{}"),
        new("Secrets", "Audit secrets", "secrets.audit", "{}"),
        new("Secrets", "Reload secrets", "secrets.reload", "{}"),
        new("Update", "Update stack", "update.run", "{}")
    ];

    public OperatorPage()
    {
        InitializeComponent();
        ParamsBox.Text = "{}";
        ConfigPatchBox.Text = "{\"ops\":[]}";
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
            MethodBox.Text = preset.Method;
            ParamsBox.Text = preset.Params;
            await RunRpcAsync();
        }
    }

    private async void RunRpc_Click(object sender, RoutedEventArgs e)
    {
        await RunRpcAsync();
    }

    private async Task RunRpcAsync()
    {
        var settings = AppSettingsStore.Load();
        var secret = GatewayCredentialStore.LoadSecret();
        StatusText.Text = $"RPC {MethodBox.Text.Trim()} verso {GatewayWebSocketService.NormalizeWebSocketUrl(settings.GatewayWsUrl, settings.GatewayUrl)}...";
        ResultBox.Text = string.Empty;
        SummaryText.Text = "Attesa risposta Gateway...";

        var result = await GatewayWebSocketService.CallAsync(
            settings,
            secret,
            MethodBox.Text,
            ParamsBox.Text);

        StatusText.Text = result.Status;
        SummaryText.Text = result.Summary;
        ResultBox.Text = string.IsNullOrWhiteSpace(result.RawJson)
            ? result.Summary
            : result.RawJson;
    }

    private async void RefreshApprovals_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "exec.approvals.get";
        ParamsBox.Text = "{}";
        await RunRpcAsync();
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
        MethodBox.Text = "exec.approval.resolve";
        ParamsBox.Text = $$"""{"id":"{{JsonEscape(ApprovalIdBox.Text)}}","decision":"{{decision}}"}""";
        await RunRpcAsync();
    }

    private async void ReadConfig_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "config.get";
        ParamsBox.Text = "{}";
        await RunRpcAsync();
    }

    private async void PatchConfig_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "config.patch";
        ParamsBox.Text = $$"""{"baseHash":"{{JsonEscape(ConfigBaseHashBox.Text)}}","patch":{{ConfigPatchBox.Text}}}""";
        await RunRpcAsync();
    }

    private async void ApplyConfig_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "config.apply";
        ParamsBox.Text = $$"""{"baseHash":"{{JsonEscape(ConfigBaseHashBox.Text)}}"}""";
        await RunRpcAsync();
    }

    private async void WorkspaceList_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "workspace.files.list";
        ParamsBox.Text = $$"""{"path":"{{JsonEscape(WorkspacePathBox.Text)}}"}""";
        await RunRpcAsync();
    }

    private async void WorkspaceRead_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "workspace.files.read";
        ParamsBox.Text = $$"""{"path":"{{JsonEscape(WorkspacePathBox.Text)}}"}""";
        await RunRpcAsync();
    }

    private async void WorkspaceWrite_Click(object sender, RoutedEventArgs e)
    {
        MethodBox.Text = "workspace.files.write";
        ParamsBox.Text = $$"""{"path":"{{JsonEscape(WorkspacePathBox.Text)}}","text":"{{JsonEscape(WorkspaceTextBox.Text)}}"}""";
        await RunRpcAsync();
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

    private sealed record OperatorPreset(string Group, string Label, string Method, string Params);
}
