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
        ProviderBox.Text = settings.Provider;
        InferenceEndpointBox.Text = settings.InferenceEndpoint;
        ModelBox.Text = settings.Model;
        DemoModeSwitch.IsOn = settings.DemoMode;
        SelectComboItem(PreferredApiBox, settings.PreferredApi);
        SelectComboItem(AccessModeBox, settings.AccessMode);
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
        StatusText.Text = "Impostazioni salvate. Pairing code non salvato per sicurezza.";
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
        LoadSettings();
        StatusText.Text = "Default ripristinati.";
    }

    private AppSettings ReadSettings()
    {
        return new AppSettings
        {
            GatewayUrl = GatewayUrlBox.Text.Trim(),
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
            ?? ValidateRequired(settings.Provider, "Provider")
            ?? ValidateHttpUrl(settings.InferenceEndpoint, "Endpoint inferenza")
            ?? ValidateRequired(settings.PreferredApi, "API preferita")
            ?? ValidateRequired(settings.Model, "Modello")
            ?? ValidateRequired(settings.AccessMode, "Accesso");
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
