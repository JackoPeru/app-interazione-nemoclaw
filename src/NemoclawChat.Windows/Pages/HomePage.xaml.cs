using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NemoclawChat_Windows.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.Foundation;
using WinRT.Interop;

namespace NemoclawChat_Windows.Pages;

public sealed partial class HomePage : Page
{
    private string _mode = "Chat";
    private string? _conversationId;
    private string? _previousResponseId;
    private readonly List<ChatMessageRecord> _messageHistory = [];
    private bool _isSending;

    public HomePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is HomeNavigationRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.ConversationId))
            {
                LoadConversation(request.ConversationId);
            }

            if (!string.IsNullOrWhiteSpace(request.Prompt))
            {
                PromptBox.Text = request.Prompt;
                PromptBox.Focus(FocusState.Programmatic);
            }
        }
        else if (e.Parameter is string prompt && !string.IsNullOrWhiteSpace(prompt))
        {
            PromptBox.Text = prompt;
            PromptBox.Focus(FocusState.Programmatic);
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        await SendCurrentPromptAsync();
    }

    private async void PromptBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && !IsShiftPressed())
        {
            e.Handled = true;
            await SendCurrentPromptAsync();
        }
    }

    private void PromptSetup_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = "Preparami i passaggi per avviare Hermes Agent API Server su Tailscale/LAN.";
    }

    private void PromptHealth_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = "Controlla stato Hermes, modello disponibile e capabilities API.";
    }

    private void PromptAgent_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = "Crea un task agente sicuro con richiesta approve/deny prima di ogni azione rischiosa.";
    }

    private async void AttachFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");

        if (App.MainWindow is not null)
        {
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        }

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            AddAction("File task", "Selezione file annullata.");
            return;
        }

        AddAction("File task", $"File selezionato: {file.Name}");
        PromptBox.Text = AppendPrompt($"Usa questo file come contesto per il task: {file.Path}");
    }

    private async void CaptureScreenshot_Click(object sender, RoutedEventArgs e)
    {
        var launched = await Launcher.LaunchUriAsync(new Uri("ms-screenclip:"));
        AddAction(
            "Screenshot",
            launched
                ? "Strumento cattura di Windows aperto. Incolla o salva lo screenshot, poi allegalo al task."
                : "Impossibile aprire lo strumento cattura. Usa una cattura manuale e allega il file al task.");
        PromptBox.Text = AppendPrompt("Usa uno screenshot come contesto visivo per capire lo stato dell'app o del server.");
    }

    private async void TakePhoto_Click(object sender, RoutedEventArgs e)
    {
        var launched = await Launcher.LaunchUriAsync(new Uri("microsoft.windows.camera:"));
        AddAction(
            "Foto",
            launched
                ? "App Fotocamera aperta. Salva la foto e allegala al task quando pronta."
                : "Impossibile aprire Fotocamera. Scatta o seleziona una foto manualmente e allegala al task.");
        PromptBox.Text = AppendPrompt("Acquisisci una foto e usala come allegato per la conversazione.");
    }

    private void VoiceNote_Click(object sender, RoutedEventArgs e)
    {
        AddAction("Voce", "Nota vocale preparata. Usa dettatura Windows o scrivi qui il testo da inviare.");
        PromptBox.Text = AppendPrompt("Trascrivi questa nota vocale e usala come contesto: ");
        PromptBox.Focus(FocusState.Programmatic);
    }

    private void CreateImage_Click(object sender, RoutedEventArgs e)
    {
        AddAction("Immagine", "Generazione immagine richiedera' tool Hermes dedicato e conferma prima di chiamate esterne.");
        PromptBox.Text = AppendPrompt("Prepara una richiesta di generazione immagine, ma chiedi conferma prima di usare tool esterni.");
    }

    private void DeepResearch_Click(object sender, RoutedEventArgs e)
    {
        AddAction("Deep Research", "Ricerca approfondita abilitera' rete solo dopo approvazione esplicita.");
        PromptBox.Text = AppendPrompt("Esegui una ricerca approfondita e cita fonti, usando rete solo dopo approvazione.");
    }

    private void WebSearch_Click(object sender, RoutedEventArgs e)
    {
        AddAction("Web", "Ricerca web marcata come azione autorizzabile: nessuna rete fuori LAN/VPN senza conferma.");
        PromptBox.Text = AppendPrompt("Cerca sul web informazioni aggiornate, chiedendo conferma prima di uscire dalla LAN/VPN.");
    }

    private void VisualExplanation_Click(object sender, RoutedEventArgs e)
    {
        AddAction("Visuale", "Spiegazione visiva richiesta: Hermes usera' blocchi statici sicuri se disponibili.");
        PromptBox.Text = AppendPrompt("Spiega anche con blocchi visuali se utile: tabella, diagramma, chart o callout. Mantieni output_text completo.");
    }

    private void Projects_Click(object sender, RoutedEventArgs e)
    {
        AddAction("Workspace", "Workspace/progetti saranno collegati ai Jobs Hermes con audit trail.");
        PromptBox.Text = AppendPrompt("Lavora sul workspace/progetto selezionato e mostra piano prima di modificare file.");
    }

    private void SetModeChat_Click(object sender, RoutedEventArgs e)
    {
        SetMode("Chat");
        AddAction("Modalita", "Chat attiva: messaggi normali, nessun task agente automatico.");
    }

    private void SetModeAgent_Click(object sender, RoutedEventArgs e)
    {
        SetMode("Agente");
        AddAction("Modalita", "Agente attivo: usa Hermes Runs/Jobs se disponibili, altrimenti fallback locale.");
    }

    private void ToggleMode_Click(object sender, RoutedEventArgs e)
    {
        SetMode(_mode == "Agente" ? "Chat" : "Agente");
    }

    private void SetMode(string mode)
    {
        _mode = mode;
        ModeBadge.Text = _mode;
        ModeToggleIcon.Glyph = _mode == "Agente" ? "\uE7BE" : "\uE8BD";
    }

    private async Task SendCurrentPromptAsync()
    {
        if (_isSending)
        {
            return;
        }

        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        _isSending = true;
        SendButton.IsEnabled = false;

        try
        {
            EmptyState.Visibility = Visibility.Collapsed;
            AddBubble("Tu", prompt, "UserBubbleBrush", HorizontalAlignment.Right);
            _messageHistory.Add(new ChatMessageRecord("Tu", prompt, DateTimeOffset.Now));
            PromptBox.Text = string.Empty;

            var settings = AppSettingsStore.Load();
            if (!settings.VisualBlocksMode.Equals("never", StringComparison.OrdinalIgnoreCase))
            {
                AddAction("Hermes", "Hermes sta preparando la risposta e gli eventuali blocchi visuali...");
            }
            var result = await GatewayService.SendChatAsync(settings, _mode, prompt, _messageHistory, _conversationId, _previousResponseId);
            AddBubble("Hermes", result.Message, "AssistantBubbleBrush", HorizontalAlignment.Left, result.VisualBlocks);
            _messageHistory.Add(new ChatMessageRecord("Hermes", result.Message, DateTimeOffset.Now, result.VisualBlocksVersion, result.VisualBlocks?.ToList()));
            var saved = ChatArchiveStore.SaveExchange(_conversationId, _mode, prompt, result.Message, result.Source, result.ResponseId, result.VisualBlocks, result.VisualBlocksVersion);
            _conversationId = saved.Id;
            _previousResponseId = saved.PreviousResponseId;

            if (result.UsedFallback || result.Source.Contains("Errore", StringComparison.OrdinalIgnoreCase))
            {
                AddAction("Stato", result.StatusMessage);
            }
        }
        finally
        {
            _isSending = false;
            SendButton.IsEnabled = true;
        }
    }

    private static bool IsShiftPressed()
    {
        return (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) &
                Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }

    private string AppendPrompt(string addition)
    {
        var current = PromptBox.Text.Trim();
        return string.IsNullOrWhiteSpace(current) ? addition : $"{current}\n{addition}";
    }

    private void AddAction(string title, string text)
    {
        EmptyState.Visibility = Visibility.Collapsed;
        AddBubble(title, text, "SurfaceBrush", HorizontalAlignment.Left);
    }

    private void LoadConversation(string conversationId)
    {
        var conversation = ChatArchiveStore.Find(conversationId);
        if (conversation is null)
        {
            return;
        }

        _conversationId = conversation.Id;
        _previousResponseId = conversation.PreviousResponseId;
        EmptyState.Visibility = Visibility.Collapsed;
        MessagesPanel.Children.Clear();
        _messageHistory.Clear();

        foreach (var message in conversation.Messages)
        {
            _messageHistory.Add(message);
            AddBubble(
                message.Author,
                message.Text,
                message.Author == "Tu" ? "UserBubbleBrush" : "AssistantBubbleBrush",
                message.Author == "Tu" ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                message.VisualBlocks);
        }
    }

    private void AddBubble(
        string author,
        string text,
        string brushKey,
        HorizontalAlignment alignment,
        IReadOnlyList<VisualBlockRecord>? visualBlocks = null)
    {
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock
        {
            Text = author,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        });
        content.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        });

        if (visualBlocks is { Count: > 0 })
        {
            foreach (var block in visualBlocks.Where(VisualBlockParser.IsValid))
            {
                content.Children.Add(RenderVisualBlock(block));
            }
        }

        var bubble = new Border
        {
            MaxWidth = 720,
            Padding = new Thickness(18, 14, 18, 14),
            CornerRadius = new CornerRadius(20),
            Background = (Brush)Application.Current.Resources[brushKey],
            BorderBrush = (Brush)Application.Current.Resources["BorderBrushSoft"],
            BorderThickness = new Thickness(1),
            HorizontalAlignment = alignment,
            Child = content
        };

        MessagesPanel.Children.Add(bubble);
        _ = MessagesScroll.ChangeView(null, MessagesScroll.ScrollableHeight, null);
    }

    private UIElement RenderVisualBlock(VisualBlockRecord block)
    {
        var panel = new StackPanel { Spacing = 8 };
        if (!string.IsNullOrWhiteSpace(block.Title))
        {
            panel.Children.Add(new TextBlock
            {
                Text = block.Title,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 15
            });
        }

        panel.Children.Add(block.Type.ToLowerInvariant() switch
        {
            "markdown" => RenderMarkdown(block.Text ?? string.Empty),
            "code" => RenderCode(block.Language ?? "plaintext", block.Code ?? string.Empty, block.Filename),
            "table" => RenderTable(block),
            "chart" => RenderChart(block),
            "diagram" => RenderDiagram(block),
            "image_gallery" => RenderGallery(block),
            "callout" => RenderCallout(block),
            _ => new TextBlock { Text = block.Caption ?? "Blocco visuale non supportato.", Foreground = (Brush)Application.Current.Resources["MutedTextBrush"] }
        });

        if (!string.IsNullOrWhiteSpace(block.Caption))
        {
            panel.Children.Add(new TextBlock
            {
                Text = block.Caption,
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                FontSize = 12,
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }

        return new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 0),
            Background = (Brush)Application.Current.Resources["SurfaceBrush"],
            BorderBrush = (Brush)Application.Current.Resources["BorderBrushSoft"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Child = panel
        };
    }

    private static StackPanel RenderMarkdown(string markdown)
    {
        var panel = new StackPanel { Spacing = 5 };
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var text = line.TrimStart('#', '-', '*', ' ');
            var block = new TextBlock
            {
                Text = line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)
                    ? $"• {text}"
                    : text,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                TextWrapping = TextWrapping.WrapWholeWords
            };

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                block.FontSize = 20;
                block.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                block.FontSize = 17;
                block.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                block.FontSize = 15;
                block.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }

            panel.Children.Add(block);
        }

        return panel;
    }

    private static UIElement RenderCode(string language, string code, string? filename)
    {
        var panel = new StackPanel { Spacing = 8 };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(filename) ? language : $"{filename} · {language}",
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
            FontSize = 12
        });
        var copy = new Button
        {
            Content = "Copia",
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        copy.Click += (_, _) =>
        {
            var package = new DataPackage();
            package.SetText(code);
            Clipboard.SetContent(package);
        };
        header.Children.Add(copy);
        panel.Children.Add(header);
        panel.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = code,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                TextWrapping = TextWrapping.NoWrap
            }
        });
        return panel;
    }

    private static UIElement RenderTable(VisualBlockRecord block)
    {
        var grid = new Grid { RowSpacing = 1, ColumnSpacing = 1 };
        foreach (var _ in block.Columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var columnIndex = 0; columnIndex < block.Columns.Count; columnIndex++)
        {
            AddTableCell(grid, block.Columns[columnIndex].Label, 0, columnIndex, true);
        }

        for (var rowIndex = 0; rowIndex < block.Rows.Count; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var columnIndex = 0; columnIndex < block.Columns.Count; columnIndex++)
            {
                var column = block.Columns[columnIndex];
                var value = block.Rows[rowIndex].TryGetValue(column.Key, out var cell)
                    ? VisualBlockParser.JsonValueToText(cell)
                    : string.Empty;
                AddTableCell(grid, value, rowIndex + 1, columnIndex, false);
            }
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = grid
        };
    }

    private static void AddTableCell(Grid grid, string text, int row, int column, bool header)
    {
        var border = new Border
        {
            Padding = new Thickness(8, 6, 8, 6),
            Background = header
                ? (Brush)Application.Current.Resources["ElevatedSurfaceBrush"]
                : (Brush)Application.Current.Resources["ComposerBrush"],
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                FontWeight = header ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                TextWrapping = TextWrapping.NoWrap
            }
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        grid.Children.Add(border);
    }

    private static UIElement RenderChart(VisualBlockRecord block)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = block.Summary ?? string.Empty,
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
            TextWrapping = TextWrapping.WrapWholeWords
        });

        var allPoints = block.Series.SelectMany(series => series.Points).ToList();
        if (allPoints.Count == 0)
        {
            return panel;
        }

        var max = Math.Max(1, allPoints.Max(point => point.Y));
        var chart = new StackPanel { Spacing = 6 };
        foreach (var point in block.Series.First().Points.Take(12))
        {
            var label = VisualBlockParser.JsonValueToText(point.X);
            var width = Math.Max(6, 420 * point.Y / max);
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = label, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 12 });
            var bar = new Border
            {
                Width = width,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = (Brush)Application.Current.Resources["AccentBrush"],
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(bar, 1);
            row.Children.Add(bar);
            var value = new TextBlock { Text = $"{point.Y:0.##}{block.Unit}", Foreground = (Brush)Application.Current.Resources["MutedTextBrush"], FontSize = 12 };
            Grid.SetColumn(value, 2);
            row.Children.Add(value);
            chart.Children.Add(row);
        }

        panel.Children.Add(chart);
        return panel;
    }

    private UIElement RenderDiagram(VisualBlockRecord block)
    {
        if (IsSafeMediaUrl(block.RenderedMediaUrl))
        {
            return new Image
            {
                Source = new BitmapImage(ResolveMediaUri(block.RenderedMediaUrl!)),
                MaxHeight = 280,
                Stretch = Stretch.Uniform
            };
        }

        return RenderCode("mermaid", block.Source ?? string.Empty, "diagram.mmd");
    }

    private UIElement RenderGallery(VisualBlockRecord block)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var image in block.Images.Take(12))
        {
            if (!IsSafeMediaUrl(image.MediaUrl))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"{image.Alt}: media non proxy rifiutato.",
                    Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                    TextWrapping = TextWrapping.WrapWholeWords
                });
                continue;
            }

            panel.Children.Add(new Image
            {
                Source = new BitmapImage(ResolveMediaUri(image.MediaUrl)),
                MaxHeight = 220,
                Stretch = Stretch.Uniform
            });
            if (!string.IsNullOrWhiteSpace(image.Caption))
            {
                panel.Children.Add(new TextBlock { Text = image.Caption, Foreground = (Brush)Application.Current.Resources["MutedTextBrush"], FontSize = 12 });
            }
        }

        return panel;
    }

    private static UIElement RenderCallout(VisualBlockRecord block)
    {
        var accent = block.Variant switch
        {
            "warning" => Microsoft.UI.Colors.Goldenrod,
            "error" => Microsoft.UI.Colors.IndianRed,
            "success" => Microsoft.UI.Colors.MediumSeaGreen,
            _ => Microsoft.UI.Colors.DodgerBlue
        };
        return new Border
        {
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 4, 0, 4),
            Child = RenderMarkdown(block.Text ?? string.Empty)
        };
    }

    private static bool IsSafeMediaUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("/v1/media/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https") || !uri.AbsolutePath.StartsWith("/v1/media/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.Host.Equals(new Uri(GatewayService.HermesRoot(AppSettingsStore.Load())).Host, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri ResolveMediaUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : new Uri($"{GatewayService.HermesRoot(AppSettingsStore.Load()).TrimEnd('/')}{value}");
    }
}
