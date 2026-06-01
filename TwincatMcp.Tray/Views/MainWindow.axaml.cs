using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TwincatMcp.Tray.Services;

namespace TwincatMcp.Tray.Views;

internal partial class MainWindow : Window
{
    private static readonly FilePickerFileType SolutionFileType = new("Visual Studio solution")
    {
        Patterns = ["*.sln"]
    };

    private readonly TrayAppController _controller;

    public MainWindow(TrayAppController controller)
    {
        InitializeComponent();
        _controller = controller;
        DataContext = _controller;
    }

    private async void BrowseSolution_Click(object? sender, RoutedEventArgs e)
    {
        string? path = await PickFileAsync("Choose TwinCAT solution", SolutionFileType);
        if (path is null)
        {
            return;
        }

        _controller.Settings.TwinCatSolutionPath = path;
        SolutionPathBox.Text = path;
        _controller.OnSettingsChanged();
    }

    private async void StartServer_Click(object? sender, RoutedEventArgs e)
    {
        await _controller.StartServerAsync();
    }

    private async void StopServer_Click(object? sender, RoutedEventArgs e)
    {
        await _controller.StopServerAsync();
    }

    private async void Apply_Click(object? sender, RoutedEventArgs e)
    {
        _controller.SaveSettings();
        await _controller.RefreshHealthAsync();
    }

    private async void CopyCodexConfig_Click(object? sender, RoutedEventArgs e)
    {
        await CopyToClipboardAsync(_controller.LocalCodexConfigToml, "Copied Codex configuration.");
    }

    private async void CopyClaudeConfig_Click(object? sender, RoutedEventArgs e)
    {
        await CopyToClipboardAsync(_controller.ClaudeCodeJson, "Copied Claude Code configuration.");
    }

    private async void CopyOtherConfig_Click(object? sender, RoutedEventArgs e)
    {
        await CopyToClipboardAsync(_controller.OtherClientInstructions, "Copied generic MCP configuration.");
    }

    private void SettingsChanged(object? sender, RoutedEventArgs e)
    {
        _controller.OnSettingsChanged();
    }

    private async Task CopyToClipboardAsync(string text, string successMessage)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            _controller.ShowNotification("Clipboard is not available.");
            return;
        }

        await clipboard.SetTextAsync(text);
        _controller.ShowNotification(successMessage);
    }

    private async Task<string?> PickFileAsync(string title, FilePickerFileType fileType)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [fileType, FilePickerFileTypes.All]
        });

        return files.Count == 0 ? null : files[0].Path.LocalPath;
    }
}
