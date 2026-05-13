using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NemoclawChat_Windows.Pages;

public sealed partial class NewsPage : Page
{
    public NewsPage()
    {
        InitializeComponent();
    }

    private void PrepareNews_Click(object sender, RoutedEventArgs e)
    {
        var prompt = NewsPromptBox.Text.Trim();
        NewsStatusText.Text = string.IsNullOrWhiteSpace(prompt)
            ? "Scrivi un brief news prima di preparare la ricerca."
            : "Ricerca pronta per Hermes: argomenti, fonti, filtri, formato briefing e frequenza.";
    }
}
