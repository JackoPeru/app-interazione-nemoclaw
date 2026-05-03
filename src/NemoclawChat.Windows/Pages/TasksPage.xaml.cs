using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NemoclawChat_Windows.Services;

namespace NemoclawChat_Windows.Pages;

public sealed partial class TasksPage : Page
{
    private readonly List<AgentTask> _tasks = new();
    private int _nextTaskId = 1;

    public TasksPage()
    {
        InitializeComponent();
        SeedTasks();
        RenderTasks();
    }

    private void QueueTask_Click(object sender, RoutedEventArgs e)
    {
        var title = TaskTitleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            TaskStatusText.Text = "Titolo task obbligatorio.";
            return;
        }

        var detail = TaskDetailBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = "Mostra piano, poi chiedi approve prima di azioni rischiose.";
        }

        _tasks.Insert(0, new AgentTask(
            _nextTaskId++,
            title,
            SelectedComboText(TaskModeBox),
            ApprovalSwitch.IsOn ? "In attesa approvazione" : "Pronto",
            detail,
            ApprovalSwitch.IsOn));

        TaskTitleBox.Text = string.Empty;
        TaskDetailBox.Text = string.Empty;
        TaskStatusText.Text = "Task accodato localmente.";
        RenderTasks();
    }

    private void LoadWorkspaceTemplate_Click(object sender, RoutedEventArgs e)
    {
        TaskTitleBox.Text = "Analizza workspace";
        TaskDetailBox.Text = "Ispeziona il progetto, individua rischi, proponi piano, poi chiedi approve prima di modificare file.";
        TaskModeBox.SelectedIndex = 2;
        ApprovalSwitch.IsOn = true;
        TaskStatusText.Text = "Template workspace caricato.";
    }

    private void LoadServerTemplate_Click(object sender, RoutedEventArgs e)
    {
        var settings = AppSettingsStore.Load();
        TaskTitleBox.Text = "Controlla home-server NemoClaw";
        TaskDetailBox.Text = $"Verifica gateway {settings.GatewayUrl}, modello {settings.Model}, sandbox e policy rete.";
        TaskModeBox.SelectedIndex = settings.DemoMode ? 0 : 1;
        ApprovalSwitch.IsOn = true;
        TaskStatusText.Text = "Template server caricato.";
    }

    private void ApproveTask_Click(object sender, RoutedEventArgs e)
    {
        UpdateTaskStatus(sender, "Approvato");
    }

    private void DenyTask_Click(object sender, RoutedEventArgs e)
    {
        UpdateTaskStatus(sender, "Negato");
    }

    private void CompleteTask_Click(object sender, RoutedEventArgs e)
    {
        UpdateTaskStatus(sender, "Completato demo");
    }

    private void UpdateTaskStatus(object sender, string status)
    {
        if (sender is not Button { Tag: int id })
        {
            return;
        }

        var index = _tasks.FindIndex(task => task.Id == id);
        if (index < 0)
        {
            return;
        }

        _tasks[index] = _tasks[index] with { Status = status };
        TaskStatusText.Text = $"Task #{id}: {status}.";
        RenderTasks();
    }

    private void SeedTasks()
    {
        if (_tasks.Count > 0)
        {
            return;
        }

        var settings = AppSettingsStore.Load();
        _tasks.Add(new AgentTask(
            _nextTaskId++,
            "Controllo gateway NemoClaw",
            settings.DemoMode ? "Demo" : "Gateway",
            "In attesa",
            $"Verifica /api/health su {settings.GatewayUrl} e modello {settings.Model}.",
            true));
    }

    private void RenderTasks()
    {
        TasksPanel.Children.Clear();

        foreach (var task in _tasks)
        {
            TasksPanel.Children.Add(CreateTaskCard(task));
        }
    }

    private UIElement CreateTaskCard(AgentTask task)
    {
        var statusBrush = task.Status.Contains("Negato", StringComparison.OrdinalIgnoreCase)
            ? new SolidColorBrush(Microsoft.UI.Colors.IndianRed)
            : (Brush)Application.Current.Resources["AccentGreenBrush"];

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = $"#{task.Id}  {task.Title}",
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 16
        });

        var status = new TextBlock
        {
            Text = task.Status,
            Foreground = statusBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(status, 1);
        header.Children.Add(status);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(CreateTaskButton("Approva", task.Id, ApproveTask_Click));
        actions.Children.Add(CreateTaskButton("Nega", task.Id, DenyTask_Click));
        actions.Children.Add(CreateTaskButton("Completa", task.Id, CompleteTask_Click));

        return new Border
        {
            Padding = new Thickness(20),
            Background = (Brush)Application.Current.Resources["AssistantBubbleBrush"],
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 58, 58, 58)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    header,
                    new TextBlock
                    {
                        Text = $"Modalita: {task.Mode} | Approve richiesto: {(task.RequiresApproval ? "si" : "no")}",
                        Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                        FontSize = 12
                    },
                    new TextBlock
                    {
                        Text = task.Detail,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    actions
                }
            }
        };
    }

    private static Button CreateTaskButton(string label, int taskId, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = label,
            Tag = taskId,
            Padding = new Thickness(12, 6, 12, 6)
        };
        button.Click += handler;
        return button;
    }

    private static string SelectedComboText(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Demo";
    }

    private sealed record AgentTask(
        int Id,
        string Title,
        string Mode,
        string Status,
        string Detail,
        bool RequiresApproval);
}
