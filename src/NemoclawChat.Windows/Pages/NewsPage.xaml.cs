using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class NewsPage : Page
{
    public NewsPage()
    {
        InitializeComponent();
        RefreshRecent();
    }

    private async void PrepareNews_Click(object sender, RoutedEventArgs e)
    {
        var prompt = NewsPromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            NewsStatusText.Text = "Scrivi un brief news prima di inviare a Hermes.";
            return;
        }

        NewsRunButton.IsEnabled = false;
        NewsStatusText.Text = "Invio a Hermes News...";
        NewsResultBox.Text = string.Empty;

        try
        {
            var result = await GatewayService.SendWorkspaceRunAsync(AppSettingsStore.Load(), "News", prompt);
            NewsStatusText.Text = result.Status;
            NewsResultBox.Text = result.Result;
            WorkspaceRequestStore.Save("News", prompt, result.Result, result.Source, result.Status);
            RefreshRecent();
        }
        finally
        {
            NewsRunButton.IsEnabled = true;
        }
    }

    private void RefreshRecent()
    {
        NewsRecentPanel.Children.Clear();
        var recent = WorkspaceRequestStore.Recent("News");
        if (recent.Count == 0)
        {
            NewsRecentPanel.Children.Add(new TextBlock
            {
                Text = "Nessun briefing news ancora.",
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["MutedTextBrush"]
            });
            return;
        }

        foreach (var item in recent)
        {
            var button = new Button
            {
                Tag = item,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = $"{item.Title} · {item.Source}"
            };
            button.Click += (_, _) =>
            {
                NewsPromptBox.Text = item.Prompt;
                NewsResultBox.Text = item.Result;
                NewsStatusText.Text = item.Status;
            };
            NewsRecentPanel.Children.Add(button);
        }
    }
}
