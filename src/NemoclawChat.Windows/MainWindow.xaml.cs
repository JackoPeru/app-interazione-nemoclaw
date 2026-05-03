using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NemoclawChat_Windows.Pages;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NemoclawChat_Windows;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        AppWindow.Title = "Nemoclaw Chat";
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void CollapseSidebar_Click(object sender, RoutedEventArgs e)
    {
        // Placeholder for compact sidebar mode.
    }

    private void NewChat_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void OpenChat_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HomePage));
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
}
