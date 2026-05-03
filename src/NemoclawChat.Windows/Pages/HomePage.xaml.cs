using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace NemoclawChat_Windows.Pages;

public sealed partial class HomePage : Page
{
    private string _mode = "Chat";

    public HomePage()
    {
        InitializeComponent();
    }

    private void Send_Click(object sender, RoutedEventArgs e)
    {
        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        AddBubble("Tu", prompt, "UserBubbleBrush");
        PromptBox.Text = string.Empty;
        var modeText = _mode == "Agente"
            ? "Creo task demo con approve/deny prima di file, rete, comandi e credenziali."
            : "Rispondo in chat demo senza avviare task agente.";
        AddBubble("NemoClaw", $"{modeText} Preset: gateway https://nemoclaw.local:8443, endpoint server http://localhost:8000/v1, API /v1/chat/completions. Quando gateway sara' attivo useremo streaming reale.", "AssistantBubbleBrush");
    }

    private void PromptSetup_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = "Preparami i passaggi per avviare NemoClaw con un endpoint OpenAI-compatible locale.";
    }

    private void PromptHealth_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = "Controlla stato gateway, modello locale e sandbox NemoClaw.";
    }

    private void PromptAgent_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = "Crea un task agente sicuro con richiesta approve/deny prima di ogni azione rischiosa.";
    }

    private void AttachFile_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = AppendPrompt("Allega un file al prossimo task e analizzalo nel contesto NemoClaw.");
    }

    private void CaptureScreenshot_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = AppendPrompt("Usa uno screenshot come contesto visivo per capire lo stato dell'app o del server.");
    }

    private void TakePhoto_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = AppendPrompt("Acquisisci una foto e usala come allegato per la conversazione.");
    }

    private void CreateImage_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = AppendPrompt("Prepara una richiesta di generazione immagine, ma chiedi conferma prima di usare tool esterni.");
    }

    private void DeepResearch_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = AppendPrompt("Esegui una ricerca approfondita e cita fonti, usando rete solo dopo approvazione.");
    }

    private void WebSearch_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = AppendPrompt("Cerca sul web informazioni aggiornate, chiedendo conferma prima di uscire dalla LAN/VPN.");
    }

    private void Projects_Click(object sender, RoutedEventArgs e)
    {
        PromptBox.Text = AppendPrompt("Lavora sul workspace/progetto selezionato e mostra piano prima di modificare file.");
    }

    private void SetModeChat_Click(object sender, RoutedEventArgs e)
    {
        _mode = "Chat";
        ModeBadge.Text = _mode;
    }

    private void SetModeAgent_Click(object sender, RoutedEventArgs e)
    {
        _mode = "Agente";
        ModeBadge.Text = _mode;
    }

    private string AppendPrompt(string addition)
    {
        var current = PromptBox.Text.Trim();
        return string.IsNullOrWhiteSpace(current) ? addition : $"{current}\n{addition}";
    }

    private void AddBubble(string author, string text, string brushKey)
    {
        var bubble = new Border
        {
            MaxWidth = 720,
            Padding = new Thickness(18, 14, 18, 14),
            CornerRadius = new CornerRadius(18),
            Background = (Brush)Application.Current.Resources[brushKey],
            HorizontalAlignment = author == "Tu" ? HorizontalAlignment.Right : HorizontalAlignment.Left,
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
    }
}
