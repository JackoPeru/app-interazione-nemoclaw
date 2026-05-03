using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;
using Windows.System;

namespace NemoclawChat_Windows.Pages;

public sealed partial class AboutPage : Page
{
    private string _releaseUrl = AppUpdateService.ReleasesPage;

    public AboutPage()
    {
        InitializeComponent();
        var settings = AppSettingsStore.Load();
        VersionText.Text = CurrentVersion;
        GatewayText.Text = settings.GatewayUrl;
        ModeText.Text = settings.DemoMode ? "Demo mode attivo" : "Connessione reale selezionata";
        SettingsPathText.Text = "Settings: %LOCALAPPDATA%\\ChatClaw\\settings.json";
    }

    private static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "0.0.0";

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusText.Text = "Controllo GitHub Releases...";
        OpenReleaseButton.IsEnabled = false;

        var result = await AppUpdateService.CheckAsync(CurrentVersion);
        _releaseUrl = result.AssetUrl ?? result.ReleaseUrl;
        UpdateStatusText.Text = result.AssetUrl is not null
            ? $"{result.Message} Apri release/asset per installare sopra la versione attuale."
            : result.Message;
        OpenReleaseButton.IsEnabled = true;
    }

    private async void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri(_releaseUrl));
    }
}
