using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class NewsPage : Page
{
    private WorkspaceRequestRecord? _selectedArticle;

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
            await GatewayService.SaveHubStateAsync(AppSettingsStore.Load(), "news_article", prompt, new
            {
                prompt,
                result = result.Result,
                status = result.Status,
                source = result.Source,
                read = false
            });
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
                _selectedArticle = item;
                NewsPromptBox.Text = item.Prompt;
                NewsResultBox.Text = item.Result;
                NewsStatusText.Text = item.Status;
                NewsFeedbackBox.Text = item.Feedback;
                _ = GatewayService.SaveHubStateAsync(AppSettingsStore.Load(), "news_read", item.Id, new { title = item.Title, read = true });
            };
            NewsRecentPanel.Children.Add(button);
        }
    }

    private void QuickNewsFeedback_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string snippet)
        {
            return;
        }

        var existing = NewsFeedbackBox.Text.Trim();
        NewsFeedbackBox.Text = string.IsNullOrWhiteSpace(existing) ? snippet : $"{existing}; {snippet}";
    }

    private async void SendNewsFeedback_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedArticle is null)
        {
            NewsStatusText.Text = "Apri un articolo prima di inviare feedback.";
            return;
        }

        var feedback = NewsFeedbackBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(feedback))
        {
            NewsStatusText.Text = "Scrivi o scegli un feedback rapido.";
            return;
        }

        NewsFeedbackButton.IsEnabled = false;
        try
        {
            var settings = AppSettingsStore.Load();
            WorkspaceRequestStore.SaveFeedback(_selectedArticle.Id, feedback, "Feedback news salvato.");
            await GatewayService.SaveHubStateAsync(settings, "news_feedback", _selectedArticle.Id, new
            {
                title = _selectedArticle.Title,
                feedback,
                read = true
            });
            var result = await GatewayService.SendWorkspaceRunAsync(settings, "News", $"Feedback sull'articolo '{_selectedArticle.Title}':\n{feedback}\n\nUsalo come memoria editoriale per migliorare i prossimi articoli.");
            NewsStatusText.Text = result.Status;
            RefreshRecent();
        }
        finally
        {
            NewsFeedbackButton.IsEnabled = true;
        }
    }
}
