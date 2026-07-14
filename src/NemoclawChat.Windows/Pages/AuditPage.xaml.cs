using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class AuditPage : Page
{
    public AuditPage() { InitializeComponent(); Loaded += async (_, _) => await RefreshAsync(); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        var risk = (RiskBox.SelectedItem as ComboBoxItem)?.Content?.ToString(); if (risk == "Tutti") risk = "";
        var query = $"?project={Uri.EscapeDataString(ProjectBox.Text)}&run={Uri.EscapeDataString(RunBox.Text)}&tool={Uri.EscapeDataString(ToolBox.Text)}&device={Uri.EscapeDataString(DeviceBox.Text)}&risk={Uri.EscapeDataString(risk ?? string.Empty)}";
        var raw = await GatewayService.SendHermesRequestAsync(AppSettingsStore.Load(), HttpMethod.Get, "/v1/hub/audit" + query);
        TimelinePanel.Children.Clear();
        try
        {
            using var document = JsonDocument.Parse(raw); var items = document.RootElement.GetProperty("items").EnumerateArray().ToList(); StatusText.Text = $"{items.Count} eventi.";
            foreach (var item in items)
            {
                var riskValue = Text(item, "risk"); var stamp = item.TryGetProperty("timestamp", out var ts) && ts.TryGetDouble(out var seconds) ? DateTimeOffset.FromUnixTimeSeconds((long)seconds).LocalDateTime.ToString("g", System.Globalization.CultureInfo.CurrentCulture) : "-";
                TimelinePanel.Children.Add(new Border { Padding = new Thickness(13), Background = (Brush)Application.Current.Resources["SurfaceBrush"], BorderBrush = new SolidColorBrush(riskValue is "high" or "critical" ? Microsoft.UI.Colors.OrangeRed : Microsoft.UI.Colors.DimGray), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = $"{stamp} · {Text(item, "event")} · rischio {riskValue}", Foreground = (Brush)Application.Current.Resources["AccentGreenBrush"], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }, new TextBlock { Text = Text(item, "summary"), Foreground = (Brush)Application.Current.Resources["TextBrush"], TextWrapping = TextWrapping.Wrap }, new TextBlock { Text = $"Progetto {Text(item, "project")} · Run {Text(item, "run")} · Tool {Text(item, "tool")} · Device {Text(item, "device")} · Stato {Text(item, "status")}", Foreground = (Brush)Application.Current.Resources["MutedTextBrush"], FontSize = 11, TextWrapping = TextWrapping.Wrap } } } });
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { StatusText.Text = $"Audit non disponibile: {ex.Message}"; }
    }
    private static string Text(JsonElement item, string name) => item.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;
}
