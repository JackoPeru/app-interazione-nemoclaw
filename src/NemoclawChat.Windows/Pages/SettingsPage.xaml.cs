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
        AppSettingsStore.Save(ReadSettings());
        StatusText.Text = "Impostazioni salvate.";
    }

    private async void TestGateway_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettings();
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
