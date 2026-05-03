using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NemoclawChat_Windows.Pages;
using NemoclawChat_Windows.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NemoclawChat_Windows;

public sealed partial class MainWindow : Window
{
    private bool _sidebarCollapsed;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        AppWindow.Title = "ChatClaw";
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        ContentFrame.Navigate(typeof(HomePage));
        ChatArchiveStore.Changed += RefreshRecentChats;
        RefreshRecentChats();
    }

    private void CollapseSidebar_Click(object sender, RoutedEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        Sidebar.Visibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = _sidebarCollapsed ? new GridLength(0) : new GridLength(280);
        RestoreSidebarButton.Visibility = _sidebarCollapsed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NewChat_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HomePage), new HomeNavigationRequest());
        RefreshRecentChats();
    }

    private void OpenChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ConversationRecord conversation })
        {
            ContentFrame.Navigate(typeof(HomePage), new HomeNavigationRequest(conversation.Id));
        }
        else if (sender is FrameworkElement { Tag: string prompt })
        {
            ContentFrame.Navigate(typeof(HomePage), new HomeNavigationRequest(Prompt: prompt));
        }
        else
        {
            ContentFrame.Navigate(typeof(HomePage), new HomeNavigationRequest());
        }
    }

    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ArchivePage));
    }

    private void Tasks_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(TasksPage));
    }

    private void Server_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(ServerPage));
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(AboutPage));
    }

    private void RefreshRecentChats()
    {
        RecentChatsPanel.Children.Clear();
        var recent = ChatArchiveStore.Recent(5);

        if (recent.Count == 0)
        {
            RecentChatsPanel.Children.Add(new TextBlock
            {
                Text = "Nessuna chat recente.",
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["MutedTextBrush"],
                Margin = new Thickness(12, 6, 12, 0)
            });
            return;
        }

        foreach (var conversation in recent)
        {
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["SidebarButtonStyle"],
                Tag = conversation,
                Content = new TextBlock
                {
                    Text = conversation.Title,
                    TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis
                }
            };
            button.Click += OpenChat_Click;
            RecentChatsPanel.Children.Add(button);
        }
    }

}
