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
        var healthUrl = $"{GatewayService.HermesRoot(_settings)}/health";
        StatusText.Text = $"Test: {healthUrl}";
        var snapshot = await GatewayService.GetServerSnapshotAsync(_settings);
        StatusText.Text = snapshot.StatusMessage;
    }

    private async void TestGatewayWs_Click(object sender, RoutedEventArgs e)
    {
        GatewayWsResultText.Text = "Lettura /v1/capabilities e /v1/models...";
        var capabilities = await GatewayService.SendHermesRequestAsync(_settings, HttpMethod.Get, "/v1/capabilities");
        var models = await GatewayService.SendHermesRequestAsync(_settings, HttpMethod.Get, "/v1/models");
        GatewayWsResultText.Text = $"Capabilities:\n{capabilities}\n\nModels:\n{models}";
    }

    private void ShowApiContract_Click(object sender, RoutedEventArgs e)
    {
        ContractText.Text =
            "GET /health -> stato Hermes API Server\n" +
            "GET /health/detailed -> stato dettagliato\n" +
            "GET /v1/models -> modelli disponibili\n" +
            "GET /v1/capabilities -> API supportate\n" +
            "POST /v1/responses -> chat primaria con store/conversation\n" +
            "POST /v1/chat/completions -> fallback OpenAI-compatible\n" +
            "POST /v1/runs -> crea run agente\n" +
            "GET /api/jobs -> lista jobs\n" +
            "POST /api/jobs -> crea job";
    }

    private async Task LoadServerSnapshotAsync()
    {
        _settings = AppSettingsStore.Load();
        var snapshot = await GatewayService.GetServerSnapshotAsync(_settings);
        GatewayText.Text = snapshot.Gateway;
        GatewayDetailText.Text = $"Health: {GatewayService.HermesRoot(_settings)}/health";
        GatewayWsText.Text = $"{_settings.GatewayUrl.TrimEnd('/')}/capabilities";
        ModelText.Text = snapshot.Model;
        ProviderText.Text = snapshot.ProviderDetail;
        InferenceText.Text = snapshot.InferenceEndpoint;
        PolicyText.Text = snapshot.Policy;
        StatusText.Text = snapshot.StatusMessage;
    }
}
