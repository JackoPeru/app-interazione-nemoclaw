using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NemoclawChat_Windows.Services;
using Windows.Storage.Pickers;
using Windows.System;
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
            var result = await GatewayService.SendChatAsync(settings, _mode, prompt, _messageHistory, _conversationId, _previousResponseId);
            AddBubble("Hermes", result.Message, "AssistantBubbleBrush", HorizontalAlignment.Left);
            _messageHistory.Add(new ChatMessageRecord("Hermes", result.Message, DateTimeOffset.Now));
            var saved = ChatArchiveStore.SaveExchange(_conversationId, _mode, prompt, result.Message, result.Source, result.ResponseId);
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
                message.Author == "Tu" ? HorizontalAlignment.Right : HorizontalAlignment.Left);
        }
    }

    private void AddBubble(string author, string text, string brushKey, HorizontalAlignment alignment)
    {
        var bubble = new Border
        {
            MaxWidth = 720,
            Padding = new Thickness(18, 14, 18, 14),
            CornerRadius = new CornerRadius(20),
            Background = (Brush)Application.Current.Resources[brushKey],
            BorderBrush = (Brush)Application.Current.Resources["BorderBrushSoft"],
            BorderThickness = new Thickness(1),
            HorizontalAlignment = alignment,
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = author,
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                    },
                    new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.WrapWholeWords,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                    }
                }
            }
        };

        MessagesPanel.Children.Add(bubble);
        _ = MessagesScroll.ChangeView(null, MessagesScroll.ScrollableHeight, null);
    }
}
