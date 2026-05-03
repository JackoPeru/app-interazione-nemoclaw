using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class ServerPage : Page
{
    public ServerPage()
    {
        InitializeComponent();
        var settings = AppSettingsStore.Load();
        GatewayText.Text = settings.GatewayUrl;
        ModelText.Text = settings.Model;
        SandboxText.Text = settings.DemoMode ? "Demo mode attivo" : "Connessione reale: attendi /api/server/status";
        PolicyText.Text = $"{settings.AccessMode}, deny by default, no segreti nel client";
    }
}
