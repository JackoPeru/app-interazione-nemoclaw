using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class VideoPage : Page
{
    public VideoPage()
    {
        InitializeComponent();
        RefreshRecent();
    }

    private async void PrepareVideo_Click(object sender, RoutedEventArgs e)
    {
        var prompt = VideoPromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            VideoStatusText.Text = "Scrivi un brief video prima di inviare a Hermes.";
            return;
        }

        VideoRunButton.IsEnabled = false;
        VideoStatusText.Text = "Invio a Hermes Video...";
        VideoResultBox.Text = string.Empty;

        try
        {
            var result = await GatewayService.SendWorkspaceRunAsync(AppSettingsStore.Load(), "Video", prompt);
            VideoStatusText.Text = result.Status;
            VideoResultBox.Text = result.Result;
            WorkspaceRequestStore.Save("Video", prompt, result.Result, result.Source, result.Status);
            RefreshRecent();
        }
        finally
        {
            VideoRunButton.IsEnabled = true;
        }
    }

    private void RefreshRecent()
    {
        VideoRecentPanel.Children.Clear();
        var recent = WorkspaceRequestStore.Recent("Video");
        if (recent.Count == 0)
        {
            VideoRecentPanel.Children.Add(new TextBlock
            {
                Text = "Nessun progetto video ancora.",
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
                VideoPromptBox.Text = item.Prompt;
                VideoResultBox.Text = item.Result;
                VideoStatusText.Text = item.Status;
            };
            VideoRecentPanel.Children.Add(button);
        }
    }
}
