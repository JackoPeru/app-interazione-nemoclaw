using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NemoclawChat_Windows.Services;
using System.Net;

namespace NemoclawChat_Windows.Pages;

public sealed partial class NewsPage : Page
{
    private IReadOnlyList<NewsHtmlRecord> _htmlPages = [];
    private NewsHtmlRecord? _selectedPage;
    private bool _webViewReady;
    private bool _fullScreenWebViewReady;
    private string? _currentHtml;
    private string? _currentBaseUrl;

    public NewsPage()
    {
        InitializeComponent();
        Loaded += NewsPage_Loaded;
    }

    private async void NewsPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= NewsPage_Loaded;
        await EnsureReaderAsync();
        await RefreshNewsHtmlAsync();
    }

    private async void RefreshNewsHtml_Click(object sender, RoutedEventArgs e)
    {
        await RefreshNewsHtmlAsync();
    }

    private async Task RefreshNewsHtmlAsync()
    {
        NewsHtmlRefreshButton.IsEnabled = false;
        NewsStatusText.Text = "Sincronizzo articoli HTML...";
        try
        {
            var result = await GatewayService.LoadNewsLibraryAsync(AppSettingsStore.Load());
            _htmlPages = result.Items;
            NewsStatusText.Text = result.Status;
            RenderCards();
            if (_selectedPage is null && _htmlPages.Count > 0)
            {
                await OpenHtmlPageAsync(_htmlPages[0]);
            }
        }
        finally
        {
            NewsHtmlRefreshButton.IsEnabled = true;
        }
    }

    private void RenderCards()
    {
        NewsCardsPanel.Children.Clear();
        if (_htmlPages.Count == 0)
        {
            var settings = AppSettingsStore.Load();
            NewsCardsPanel.Children.Add(new TextBlock
            {
                Text = $"Nessun articolo HTML trovato. Chiedi a Hermes di salvare il giornale in {settings.NewsLibraryPath}.",
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var page in _htmlPages)
        {
            var card = CreateArticleCard(page);
            NewsCardsPanel.Children.Add(card);
        }
    }

    private Button CreateArticleCard(NewsHtmlRecord page)
    {
        var title = new TextBlock
        {
            Text = page.Title,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxLines = 2
        };
        var meta = new TextBlock
        {
            Text = $"{page.ModifiedAt.LocalDateTime:g} · {FormatSize(page.SizeBytes)}",
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap
        };
        var file = new TextBlock
        {
            Text = page.FileName,
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(title);
        stack.Children.Add(meta);
        stack.Children.Add(file);

        var button = new Button
        {
            Tag = page,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 12, 14, 12),
            Content = stack
        };
        button.Click += async (_, _) => await OpenHtmlPageAsync(page);
        return button;
    }

    private async Task OpenHtmlPageAsync(NewsHtmlRecord page)
    {
        _selectedPage = page;
        NewsReaderTitleText.Text = page.Title;
        NewsReaderMetaText.Text = $"{page.FileName} · {page.ModifiedAt.LocalDateTime:g} · {FormatSize(page.SizeBytes)}";
        NewsStatusText.Text = $"Apro {page.Title}";

        try
        {
            await EnsureReaderAsync();
            var html = await GatewayService.LoadGatewayTextAsync(AppSettingsStore.Load(), page.Url);
            _currentHtml = html;
            _currentBaseUrl = page.Url;
            OpenNewsFullScreenButton.IsEnabled = true;
            await RenderCurrentHtmlAsync();
            NewsStatusText.Text = "Articolo aperto.";
            await GatewayService.SaveHubStateAsync(AppSettingsStore.Load(), "news_read", page.Id, new { title = page.Title, file = page.FileName, read = true });
        }
        catch (Exception ex)
        {
            NewsStatusText.Text = $"Errore apertura articolo: {ex.Message}";
            _currentHtml = null;
            _currentBaseUrl = null;
            OpenNewsFullScreenButton.IsEnabled = false;
            if (_webViewReady)
            {
                NewsWebView.NavigateToString(ErrorHtml(ex.Message));
            }
            if (_fullScreenWebViewReady)
            {
                FullScreenNewsWebView.NavigateToString(ErrorHtml(ex.Message));
            }
        }
    }

    private async Task EnsureReaderAsync()
    {
        if (_webViewReady)
        {
            return;
        }

        await NewsWebView.EnsureCoreWebView2Async();
        _webViewReady = true;
        NewsWebView.NavigateToString("<!doctype html><html><body style=\"font-family:Segoe UI,sans-serif;background:#111318;color:#e5e7eb;padding:32px\"><h1>News</h1><p>Seleziona un articolo.</p></body></html>");
    }

    private async Task EnsureFullScreenReaderAsync()
    {
        if (_fullScreenWebViewReady)
        {
            return;
        }

        await FullScreenNewsWebView.EnsureCoreWebView2Async();
        _fullScreenWebViewReady = true;
        FullScreenNewsWebView.NavigateToString("<!doctype html><html><body style=\"font-family:Segoe UI,sans-serif;background:#111318;color:#e5e7eb;padding:32px\"><h1>News</h1><p>Apri un articolo per usare il fullscreen.</p></body></html>");
    }

    private async Task RenderCurrentHtmlAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentBaseUrl))
        {
            return;
        }

        var rendered = InjectBaseHref(_currentHtml ?? string.Empty, _currentBaseUrl);
        NewsWebView.NavigateToString(rendered);
        if (FullScreenNewsOverlay.Visibility == Visibility.Visible)
        {
            await EnsureFullScreenReaderAsync();
            FullScreenNewsWebView.NavigateToString(rendered);
        }
    }

    private async void OpenNewsFullScreen_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPage is null || string.IsNullOrWhiteSpace(_currentBaseUrl))
        {
            NewsStatusText.Text = "Apri prima un articolo HTML.";
            return;
        }

        await EnsureFullScreenReaderAsync();
        FullScreenNewsTitleText.Text = _selectedPage.Title;
        FullScreenNewsMetaText.Text = $"{_selectedPage.FileName} · {_selectedPage.ModifiedAt.LocalDateTime:g} · {FormatSize(_selectedPage.SizeBytes)}";
        FullScreenNewsOverlay.Visibility = Visibility.Visible;
        await RenderCurrentHtmlAsync();
    }

    private void CloseNewsFullScreen_Click(object sender, RoutedEventArgs e)
    {
        FullScreenNewsOverlay.Visibility = Visibility.Collapsed;
    }

    private static string InjectBaseHref(string html, string baseUrl)
    {
        var safeBase = WebUtility.HtmlEncode(baseUrl);
        var baseTag = $"<base href=\"{safeBase}\">";
        var headIndex = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        if (headIndex >= 0)
        {
            var headEnd = html.IndexOf('>', headIndex);
            if (headEnd >= 0)
            {
                return html.Insert(headEnd + 1, baseTag);
            }
        }

        return $"<!doctype html><html><head>{baseTag}<meta charset=\"utf-8\"></head><body>{html}</body></html>";
    }

    private static string ErrorHtml(string message) =>
        $"<!doctype html><html><body style=\"font-family:Segoe UI,sans-serif;background:#111318;color:#e5e7eb;padding:32px\"><h1>Errore News</h1><p>{WebUtility.HtmlEncode(message)}</p></body></html>";

    private static string FormatSize(long sizeBytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = Math.Max(0, sizeBytes);
        var unit = 0;
        double display = value;
        while (display >= 1024 && unit < units.Length - 1)
        {
            display /= 1024;
            unit++;
        }

        return $"{display:0.#} {units[unit]}";
    }
}
