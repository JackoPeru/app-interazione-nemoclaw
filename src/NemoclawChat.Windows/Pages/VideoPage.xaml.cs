using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NemoclawChat_Windows.Pages;

public sealed partial class VideoPage : Page
{
    public VideoPage()
    {
        InitializeComponent();
    }

    private void PrepareVideo_Click(object sender, RoutedEventArgs e)
    {
        var prompt = VideoPromptBox.Text.Trim();
        VideoStatusText.Text = string.IsNullOrWhiteSpace(prompt)
            ? "Scrivi un brief video prima di preparare la richiesta."
            : "Richiesta pronta per Hermes: script, storyboard, asset necessari e piano generazione video.";
    }
}
