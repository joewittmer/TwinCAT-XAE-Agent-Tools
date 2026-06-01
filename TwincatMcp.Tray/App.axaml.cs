using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TwincatMcp.Tray.Services;
using TwincatMcp.Tray.Views;

namespace TwincatMcp.Tray;

public partial class App : Application
{
    private TrayAppController? _controller;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _controller = new TrayAppController(ShowSettings, Shutdown);
            DataContext = _controller;

            desktop.Exit += (_, _) =>
            {
                _controller.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowSettings()
    {
        if (_controller is null)
        {
            return;
        }

        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow(_controller);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void Shutdown()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
