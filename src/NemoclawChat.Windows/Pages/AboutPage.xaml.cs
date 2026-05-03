using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var settings = AppSettingsStore.Load();
        VersionText.Text = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Debug locale";
        GatewayText.Text = settings.GatewayUrl;
        ModeText.Text = settings.DemoMode ? "Demo mode attivo" : "Connessione reale selezionata";
        SettingsPathText.Text = "Settings: %LOCALAPPDATA%\\NemoclawChat\\settings.json";
    }
}
