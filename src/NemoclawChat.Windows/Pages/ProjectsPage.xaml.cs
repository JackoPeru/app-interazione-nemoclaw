using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NemoclawChat_Windows.Services;
using System.Globalization;

namespace NemoclawChat_Windows.Pages;

public sealed partial class ProjectsPage : Page
{
    private ConversationRecord? _selected;

    public ProjectsPage()
    {
        InitializeComponent();
        Loaded += ProjectsPage_Loaded;
    }

    private void ProjectsPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ProjectsPage_Loaded;
        RenderProjectList();
        var activeId = AppSettingsStore.Load().ActiveProjectId;
        var active = ChatArchiveStore.Projects().FirstOrDefault(project => project.Id.Equals(activeId, StringComparison.OrdinalIgnoreCase));
        if (active is not null)
        {
            SelectProject(active);
        }
    }

    private void RenderProjectList()
    {
        ProjectListPanel.Children.Clear();
        var projects = ChatArchiveStore.Projects();
        if (projects.Count == 0)
        {
            ProjectListPanel.Children.Add(new TextBlock
            {
                Text = "Nessun progetto. Creane uno per legare chat, workspace e contesto Hermes.",
                Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var activeId = AppSettingsStore.Load().ActiveProjectId;
        foreach (var project in projects)
        {
            var button = new Button
            {
                Tag = project,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12),
                Content = new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock { Text = project.Title, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock
                        {
                            Text = project.Id.Equals(activeId, StringComparison.OrdinalIgnoreCase) ? "Attivo" : $"Aggiornato {project.UpdatedAt.LocalDateTime:g}",
                            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                            FontSize = 11
                        }
                    }
                }
            };
            button.Click += Project_Click;
            ProjectListPanel.Children.Add(button);
        }
    }

    private void Project_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConversationRecord project })
        {
            SelectProject(project);
        }
    }

    private void SelectProject(ConversationRecord project)
    {
        _selected = project;
        DashboardTitle.Text = project.Title;
        TitleBox.Text = project.Title;
        DescriptionBox.Text = project.Description;
        WorkspacePathBox.Text = project.WorkspacePath;
        RepositoryUrlBox.Text = project.RepositoryUrl;
        InstructionsBox.Text = project.ProjectInstructions;
        MemoryBox.Text = project.ProjectMemory;
        ToolsBox.Text = string.Join(Environment.NewLine, project.AuthorizedTools);
        ActivateButton.IsEnabled = true;
        NewChatButton.IsEnabled = true;
        RenderDashboard(project);
    }

    private void RenderDashboard(ConversationRecord project)
    {
        var settings = AppSettingsStore.Load();
        ActiveBadge.Text = settings.ActiveProjectId.Equals(project.Id, StringComparison.OrdinalIgnoreCase) ? "PROGETTO ATTIVO" : string.Empty;
        var conversations = ChatArchiveStore.ForProject(project.Id);
        var artifacts = conversations
            .SelectMany(conversation => conversation.Messages.SelectMany(message => message.VisualBlocks ?? []))
            .Where(block => block.Type is "media_file" or "image_gallery" or "code" or "diagram" or "markdown" or "table" or "chart")
            .ToList();
        ChatCountText.Text = conversations.Count.ToString(CultureInfo.InvariantCulture);
        ArtifactCountText.Text = artifacts.Count.ToString(CultureInfo.InvariantCulture);
        ActivityCountText.Text = conversations.Sum(conversation => conversation.Messages.Count).ToString(CultureInfo.InvariantCulture);

        ProjectChatsPanel.Children.Clear();
        foreach (var conversation in conversations.Take(12))
        {
            var button = new Button
            {
                Tag = conversation.Id,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = $"{conversation.Title}  ·  {conversation.UpdatedAt.LocalDateTime:g}"
            };
            button.Click += OpenConversation_Click;
            ProjectChatsPanel.Children.Add(button);
        }
        if (ProjectChatsPanel.Children.Count == 0)
        {
            ProjectChatsPanel.Children.Add(EmptyText("Nessuna chat associata. Attiva progetto e crea nuova chat."));
        }

        ProjectArtifactsPanel.Children.Clear();
        foreach (var artifact in artifacts.Take(12))
        {
            ProjectArtifactsPanel.Children.Add(new TextBlock
            {
                Text = $"{artifact.Type} · {artifact.Title ?? artifact.Filename ?? artifact.Id}",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                TextWrapping = TextWrapping.Wrap
            });
        }
        if (ProjectArtifactsPanel.Children.Count == 0)
        {
            ProjectArtifactsPanel.Children.Add(EmptyText("Nessun artifact prodotto nelle chat del progetto."));
        }
    }

    private static TextBlock EmptyText(string text) => new()
    {
        Text = text,
        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
        TextWrapping = TextWrapping.Wrap
    };

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        _selected = null;
        DashboardTitle.Text = "Nuovo progetto";
        ActiveBadge.Text = string.Empty;
        TitleBox.Text = string.Empty;
        DescriptionBox.Text = string.Empty;
        WorkspacePathBox.Text = string.Empty;
        RepositoryUrlBox.Text = string.Empty;
        InstructionsBox.Text = string.Empty;
        MemoryBox.Text = string.Empty;
        ToolsBox.Text = string.Empty;
        ActivateButton.IsEnabled = false;
        NewChatButton.IsEnabled = false;
        ChatCountText.Text = "0";
        ArtifactCountText.Text = "0";
        ActivityCountText.Text = "0";
        ProjectChatsPanel.Children.Clear();
        ProjectArtifactsPanel.Children.Clear();
        StatusText.Text = "Inserisci dati progetto.";
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var tools = ToolsBox.Text.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var project = ChatArchiveStore.SaveProjectWorkspace(
                _selected?.Id,
                TitleBox.Text,
                DescriptionBox.Text,
                WorkspacePathBox.Text,
                RepositoryUrlBox.Text,
                InstructionsBox.Text,
                MemoryBox.Text,
                tools);
            _selected = project;
            var settings = AppSettingsStore.Load();
            if (settings.ActiveProjectId.Equals(project.Id, StringComparison.OrdinalIgnoreCase))
            {
                settings.ActiveProjectName = project.Title;
                AppSettingsStore.Save(settings);
            }
            RenderProjectList();
            SelectProject(project);
            StatusText.Text = "Progetto salvato. Sync gateway in coda.";
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void ActivateProject_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var settings = AppSettingsStore.Load();
        settings.ActiveProjectId = _selected.Id;
        settings.ActiveProjectName = _selected.Title;
        AppSettingsStore.Save(settings);
        RenderProjectList();
        RenderDashboard(_selected);
        StatusText.Text = "Progetto attivo. Nuove chat riceveranno contesto workspace automaticamente.";
    }

    private void DeactivateProject_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppSettingsStore.Load();
        settings.ActiveProjectId = string.Empty;
        settings.ActiveProjectName = string.Empty;
        AppSettingsStore.Save(settings);
        RenderProjectList();
        if (_selected is not null) RenderDashboard(_selected);
        StatusText.Text = "Nessun progetto attivo.";
    }

    private void NewProjectChat_Click(object sender, RoutedEventArgs e)
    {
        ActivateProject_Click(sender, e);
        Frame.Navigate(typeof(HomePage), new HomeNavigationRequest());
    }

    private void OpenConversation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            Frame.Navigate(typeof(HomePage), new HomeNavigationRequest(id));
        }
    }
}
