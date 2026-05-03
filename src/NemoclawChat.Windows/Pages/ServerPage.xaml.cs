using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class ServerPage : Page
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private AppSettings _settings = new();

    public ServerPage()
    {
        InitializeComponent();
        LoadServerSnapshot();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadServerSnapshot();
    }

    private async void TestGateway_Click(object sender, RoutedEventArgs e)
    {
        var healthUrl = $"{_settings.GatewayUrl.TrimEnd('/')}/api/health";
        StatusText.Text = $"Test: {healthUrl}";

        try
        {
            using var response = await HttpClient.GetAsync(healthUrl);
            StatusText.Text = response.IsSuccessStatusCode
                ? "Gateway raggiungibile."
                : $"Gateway risponde: HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Gateway non raggiungibile: {ex.Message}";
        }
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

    private void LoadServerSnapshot()
    {
        _settings = AppSettingsStore.Load();
        GatewayText.Text = _settings.GatewayUrl;
        GatewayDetailText.Text = $"Health: {_settings.GatewayUrl.TrimEnd('/')}/api/health";
        ModelText.Text = _settings.Model;
        ProviderText.Text = $"Provider: {_settings.Provider} | API: {_settings.PreferredApi}";
        InferenceText.Text = _settings.InferenceEndpoint;
        PolicyText.Text = _settings.AccessMode;
        StatusText.Text = _settings.DemoMode
            ? "Demo mode attivo. Nessuna chiamata automatica al server."
            : "Connessione reale selezionata. Usa Test gateway.";
    }
}
