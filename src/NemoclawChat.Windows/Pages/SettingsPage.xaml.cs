using System.Net.Http;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class SettingsPage : Page
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = AppSettingsStore.Load();
        GatewayUrlBox.Text = settings.GatewayUrl;
        GatewayWsUrlBox.Text = GatewayWebSocketService.NormalizeWebSocketUrl(settings.GatewayWsUrl, settings.GatewayUrl);
        AdminBridgeUrlBox.Text = settings.AdminBridgeUrl;
        ProviderBox.Text = settings.Provider;
        InferenceEndpointBox.Text = settings.InferenceEndpoint;
        ModelBox.Text = settings.Model;
        DemoModeSwitch.IsOn = settings.DemoMode;
        SelectComboItem(PreferredApiBox, settings.PreferredApi);
        SelectComboItem(AccessModeBox, settings.AccessMode);
        PairingCodeBox.PlaceholderText = GatewayCredentialStore.HasSecret()
            ? "Segreto salvato nel Credential Locker"
            : "Token/password o pairing code Gateway";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettings();
        var error = Validate(settings);
        if (error is not null)
        {
            StatusText.Text = error;
            return;
        }

        AppSettingsStore.Save(settings);
        if (!string.IsNullOrWhiteSpace(PairingCodeBox.Password))
        {
            GatewayCredentialStore.SaveSecret(PairingCodeBox.Password);
            PairingCodeBox.Password = string.Empty;
        }

        StatusText.Text = GatewayCredentialStore.HasSecret()
            ? "Impostazioni salvate. Segreto Gateway salvato in Credential Locker."
            : "Impostazioni salvate. Nessun segreto Gateway salvato.";
    }

    private async void TestGatewayWs_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettings();
        var error = ValidateWebSocket(settings.GatewayWsUrl);
        if (error is not null)
        {
            StatusText.Text = error;
            return;
        }

        StatusText.Text = $"Test WS: {settings.GatewayWsUrl}";
        var probe = await GatewayWebSocketService.ProbeAsync(settings, ReadGatewaySecret());
        StatusText.Text = $"{probe.Status} {probe.Details}";
    }

    private async void TestGateway_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettings();
        var error = ValidateGateway(settings.GatewayUrl);
        if (error is not null)
        {
            StatusText.Text = error;
            return;
        }

        var healthUrl = $"{settings.GatewayUrl.TrimEnd('/')}/api/health";
        StatusText.Text = $"Test: {healthUrl}";

        try
        {
            using var response = await HttpClient.GetAsync(healthUrl);
            StatusText.Text = response.IsSuccessStatusCode
                ? "Gateway raggiungibile."
                : $"Gateway risponde: {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Gateway non raggiungibile: {ex.Message}";
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        AppSettingsStore.Reset();
        GatewayCredentialStore.DeleteSecret();
        LoadSettings();
        StatusText.Text = "Default ripristinati.";
    }

    private string ReadGatewaySecret()
    {
        return string.IsNullOrWhiteSpace(PairingCodeBox.Password)
            ? GatewayCredentialStore.LoadSecret()
            : PairingCodeBox.Password;
    }

    private AppSettings ReadSettings()
    {
        return new AppSettings
        {
            GatewayUrl = GatewayUrlBox.Text.Trim(),
            GatewayWsUrl = GatewayWebSocketService.NormalizeWebSocketUrl(GatewayWsUrlBox.Text.Trim(), GatewayUrlBox.Text.Trim()),
            AdminBridgeUrl = AdminBridgeUrlBox.Text.Trim(),
            Provider = ProviderBox.Text.Trim(),
            InferenceEndpoint = InferenceEndpointBox.Text.Trim(),
            PreferredApi = SelectedComboText(PreferredApiBox),
            Model = ModelBox.Text.Trim(),
            AccessMode = SelectedComboText(AccessModeBox),
            DemoMode = DemoModeSwitch.IsOn
        };
    }

    private static string? Validate(AppSettings settings)
    {
        return ValidateGateway(settings.GatewayUrl)
            ?? ValidateWebSocket(settings.GatewayWsUrl)
            ?? ValidateHttpUrl(settings.AdminBridgeUrl, "Admin Bridge URL")
            ?? ValidateRequired(settings.Provider, "Provider")
            ?? ValidateHttpUrl(settings.InferenceEndpoint, "Endpoint inferenza")
            ?? ValidateRequired(settings.PreferredApi, "API preferita")
            ?? ValidateRequired(settings.Model, "Modello")
            ?? ValidateRequired(settings.AccessMode, "Accesso");
    }

    private static string? ValidateWebSocket(string gatewayWsUrl)
    {
        if (string.IsNullOrWhiteSpace(gatewayWsUrl))
        {
            return "Gateway WebSocket URL obbligatorio.";
        }

        if (!Uri.TryCreate(gatewayWsUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            return "Gateway WebSocket URL deve essere ws/wss valido.";
        }

        return null;
    }

    private static string? ValidateGateway(string gatewayUrl)
    {
        return ValidateHttpUrl(gatewayUrl, "Gateway URL");
    }

    private static string? ValidateHttpUrl(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{label} obbligatorio.";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return $"{label} deve essere URL http/https valido.";
        }

        return null;
    }

    private static string? ValidateRequired(string value, string label)
    {
        return string.IsNullOrWhiteSpace(value) ? $"{label} obbligatorio." : null;
    }

    private static string SelectedComboText(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
    }

    private static void SelectComboItem(ComboBox comboBox, string value)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }
}
