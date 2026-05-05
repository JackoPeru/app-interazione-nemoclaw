using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class ServerPage : Page
{
    private AppSettings _settings = new();

    public ServerPage()
    {
        InitializeComponent();
        _ = LoadServerSnapshotAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadServerSnapshotAsync();
    }

    private async void TestGateway_Click(object sender, RoutedEventArgs e)
    {
        var healthUrl = $"{_settings.GatewayUrl.TrimEnd('/')}/api/health";
        StatusText.Text = $"Test: {healthUrl}";
        var snapshot = await GatewayService.GetServerSnapshotAsync(_settings);
        StatusText.Text = snapshot.StatusMessage;
    }

    private async void TestGatewayWs_Click(object sender, RoutedEventArgs e)
    {
        GatewayWsResultText.Text = "Connessione WS e handshake OpenClaw...";
        var secret = string.IsNullOrWhiteSpace(GatewaySecretBox.Password)
            ? GatewayCredentialStore.LoadSecret()
            : GatewaySecretBox.Password;
        var probe = await GatewayWebSocketService.ProbeAsync(_settings, secret);
        GatewayWsResultText.Text =
            $"{probe.Status}\n{probe.Details}\n\n" +
            (probe.CapabilityLines.Count == 0
                ? "Nessuna RPC capability letta."
                : string.Join("\n", probe.CapabilityLines));
    }

    private void ShowApiContract_Click(object sender, RoutedEventArgs e)
    {
        ContractText.Text =
            "GET /api/health -> stato gateway\n" +
            "GET /api/server/status -> modello, provider, sandbox, policy rete\n" +
            "POST /api/chat/stream -> chat streaming OpenAI-compatible verso IA locale\n" +
            "POST /api/tasks -> crea task agente con audit trail\n" +
            "POST /api/tasks/{id}/approve -> approva file, rete o comandi\n" +
            "POST /api/tasks/{id}/deny -> blocca azione rischiosa";
    }

    private async Task LoadServerSnapshotAsync()
    {
        _settings = AppSettingsStore.Load();
        var snapshot = await GatewayService.GetServerSnapshotAsync(_settings);
        GatewayText.Text = snapshot.Gateway;
        GatewayDetailText.Text = $"Health: {snapshot.Gateway.TrimEnd('/')}/api/health";
        GatewayWsText.Text = GatewayWebSocketService.NormalizeWebSocketUrl(_settings.GatewayWsUrl, _settings.GatewayUrl);
        ModelText.Text = snapshot.Model;
        ProviderText.Text = snapshot.ProviderDetail;
        InferenceText.Text = snapshot.InferenceEndpoint;
        PolicyText.Text = snapshot.Policy;
        StatusText.Text = snapshot.StatusMessage;
    }
}
