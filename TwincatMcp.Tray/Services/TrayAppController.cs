using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using TwincatMcp.Tray.Models;

namespace TwincatMcp.Tray.Services;

internal sealed class TrayAppController : INotifyPropertyChanged, IDisposable
{
    private readonly WindowIcon _inactiveTrayIcon = LoadTrayIcon("tray.ico");
    private readonly WindowIcon _runningTrayIcon = LoadTrayIcon("tray-running.ico");
    private readonly Action _showSettings;
    private readonly Action _exit;
    private readonly SettingsStore _settingsStore = new();
    private readonly CodexConfigStore _codexConfigStore = new();
    private readonly ServerProcessManager _serverProcess = new();
    private readonly ServerHealthClient _healthClient = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _notificationTimer;
    private bool _isServerHealthy;
    private bool _isNotificationVisible;
    private string _status = "Stopped";
    private string _notificationMessage = string.Empty;

    public TrayAppController(Action showSettings, Action exit)
    {
        _showSettings = showSettings;
        _exit = exit;
        Settings = _settingsStore.Load();

        ShowSettingsCommand = new RelayCommand(_showSettings);
        StartServerCommand = new RelayCommand(StartServerAsync);
        StopServerCommand = new RelayCommand(StopServerAsync);
        UpdateCodexConfigCommand = new RelayCommand(UpdateCodexConfig);
        ExitCommand = new RelayCommand(ExitAsync);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _timer.Tick += async (_, _) => await RefreshHealthAsync();
        _timer.Start();

        _notificationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _notificationTimer.Tick += (_, _) => HideNotification();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TraySettings Settings { get; }

    public ICommand ShowSettingsCommand { get; }

    public ICommand StartServerCommand { get; }

    public ICommand StopServerCommand { get; }

    public ICommand UpdateCodexConfigCommand { get; }

    public ICommand ExitCommand { get; }

    public bool IsServerHealthy
    {
        get => _isServerHealthy;
        private set
        {
            if (_isServerHealthy == value)
            {
                return;
            }

            _isServerHealthy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TrayIcon));
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (string.Equals(_status, value, StringComparison.Ordinal))
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentConfigurationText));
            OnPropertyChanged(nameof(TrayToolTip));
        }
    }

    public bool IsNotificationVisible
    {
        get => _isNotificationVisible;
        private set
        {
            if (_isNotificationVisible == value)
            {
                return;
            }

            _isNotificationVisible = value;
            OnPropertyChanged();
        }
    }

    public string NotificationMessage
    {
        get => _notificationMessage;
        private set
        {
            if (string.Equals(_notificationMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _notificationMessage = value;
            OnPropertyChanged();
        }
    }

    public string McpUrl => Settings.LocalMcpUrl;

    public string ProductName => "TwinCAT XAE Agent Tools";

    public string ProductDescription => "MCP server for TwinCAT XAE automation and bounded workspace access.";

    public string AppVersion => GetAppVersion();

    public string BindAddress => Settings.BindAddress;

    public string RemoteMcpUrl => $"http://{{IP_ADDRESS}}:{Settings.Port}/mcp";

    public WindowIcon TrayIcon => IsServerHealthy ? _runningTrayIcon : _inactiveTrayIcon;

    public string TrayToolTip => $"TwinCAT XAE Agent Tools - {Status}";

    public string SettingsPath => _settingsStore.SettingsPath;

    public string CodexConfigPath => _codexConfigStore.ConfigPath;

    public string LocalCodexConfigToml => ClientConfigurationText.BuildCodexToml(McpUrl);

    public string RemoteCodexConfigToml => ClientConfigurationText.BuildCodexToml(RemoteMcpUrl);

    public string RemoteCodexInstructions => ClientConfigurationText.BuildRemoteCodexInstructions(McpUrl);

    public string ClaudeCodeCommand => ClientConfigurationText.BuildClaudeCodeCommand(McpUrl);

    public string ClaudeCodeJson => ClientConfigurationText.BuildClaudeCodeJson(McpUrl);

    public string ClaudeCodeInstructions => ClientConfigurationText.BuildClaudeCodeInstructions(McpUrl);

    public string OtherClientInstructions => ClientConfigurationText.BuildOtherClientInstructions(McpUrl);

    public string CurrentConfigurationText =>
        $"Status: {Status}{Environment.NewLine}" +
        $"Server bind: {BindAddress}:{Settings.Port}{Environment.NewLine}" +
        $"Local MCP URL: {McpUrl}{Environment.NewLine}" +
        $"Remote MCP URL: {RemoteMcpUrl}{Environment.NewLine}" +
        $"LAN access: {GetLanAccessText()}{Environment.NewLine}" +
        $"TwinCAT solution: {BlankToNotSet(Settings.TwinCatSolutionPath)}{Environment.NewLine}" +
        $"Workspace root: {GetWorkspaceRootText()}{Environment.NewLine}" +
        $"XAE ProgID: {Settings.XaeProgId}{Environment.NewLine}" +
        $"Project load timeout: {Settings.ProjectLoadTimeoutSeconds} seconds{Environment.NewLine}" +
        $"Tray settings: {SettingsPath}{Environment.NewLine}" +
        $"Codex config: {CodexConfigPath}{Environment.NewLine}";

    public void SaveSettings(bool showNotification = true)
    {
        NormalizeSettings();
        _settingsStore.Save(Settings);
        OnSettingsChanged();
        if (showNotification)
        {
            ShowNotification($"Saved settings to {_settingsStore.SettingsPath}");
        }
    }

    public async Task StartServerAsync()
    {
        try
        {
            SaveSettings(showNotification: false);
            Status = "Starting";
            await _serverProcess.StartAsync(Settings);
            await RefreshHealthAsync();
            ShowNotification("TwinCAT XAE Agent Tools server started.");
        }
        catch (Exception ex)
        {
            Status = "Start failed";
            ShowNotification(ex.Message);
        }
    }

    public async Task StopServerAsync()
    {
        await _serverProcess.StopAsync();
        IsServerHealthy = false;
        Status = "Stopped";
        ShowNotification("TwinCAT XAE Agent Tools server stopped.");
    }

    public void UpdateCodexConfig()
    {
        try
        {
            SaveSettings(showNotification: false);
            _codexConfigStore.SaveTwinCatServer(McpUrl);
            ShowNotification($"Updated Codex config at {CodexConfigPath}. Restart Codex to load TwinCAT XAE Agent Tools.");
        }
        catch (Exception ex)
        {
            ShowNotification($"Could not update Codex config: {ex.Message}");
        }
    }

    public async Task RefreshHealthAsync()
    {
        IsServerHealthy = await _healthClient.IsHealthyAsync(Settings, CancellationToken.None);

        if (IsServerHealthy)
        {
            Status = "Running";
            return;
        }

        Status = _serverProcess.IsRunning ? "Starting or unavailable" : "Stopped";
    }

    public void OnSettingsChanged()
    {
        OnPropertyChanged(nameof(McpUrl));
        OnPropertyChanged(nameof(BindAddress));
        OnPropertyChanged(nameof(RemoteMcpUrl));
        OnPropertyChanged(nameof(LocalCodexConfigToml));
        OnPropertyChanged(nameof(RemoteCodexConfigToml));
        OnPropertyChanged(nameof(RemoteCodexInstructions));
        OnPropertyChanged(nameof(ClaudeCodeCommand));
        OnPropertyChanged(nameof(ClaudeCodeJson));
        OnPropertyChanged(nameof(ClaudeCodeInstructions));
        OnPropertyChanged(nameof(OtherClientInstructions));
        OnPropertyChanged(nameof(CurrentConfigurationText));
    }

    public void Dispose()
    {
        _timer.Stop();
        _notificationTimer.Stop();
        _serverProcess.Dispose();
        _healthClient.Dispose();
    }

    private async Task ExitAsync()
    {
        await StopServerAsync();
        _exit();
    }

    private void NormalizeSettings()
    {
        if (Settings.Port <= 0)
        {
            Settings.Port = 5001;
        }

        if (string.IsNullOrWhiteSpace(Settings.XaeProgId))
        {
            Settings.XaeProgId = "TcXaeShell.DTE.17.0";
        }

        if (Settings.ProjectLoadTimeoutSeconds <= 0)
        {
            Settings.ProjectLoadTimeoutSeconds = 30;
        }
    }

    public void ShowNotification(string message)
    {
        NotificationMessage = message;
        IsNotificationVisible = true;
        _notificationTimer.Stop();
        _notificationTimer.Start();
    }

    private void HideNotification()
    {
        _notificationTimer.Stop();
        IsNotificationVisible = false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static WindowIcon LoadTrayIcon(string fileName)
    {
        Uri iconUri = new($"avares://TwincatMcp.Tray/Assets/{fileName}");
        using Stream iconStream = AssetLoader.Open(iconUri);
        return new WindowIcon(iconStream);
    }

    private static string BlankToNotSet(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(not set)" : value;
    }

    private static string GetAppVersion()
    {
        Assembly assembly = typeof(TrayAppController).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        Version? assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is not null)
        {
            return assemblyVersion.ToString(3);
        }

        return "unknown";
    }

    private string GetLanAccessText()
    {
        return Settings.AllowLanAccess
            ? "Enabled"
            : "Disabled - enable it before using the remote URL";
    }

    private string GetWorkspaceRootText()
    {
        if (!string.IsNullOrWhiteSpace(Settings.TwinCatSolutionPath))
        {
            string? directory = Path.GetDirectoryName(Settings.TwinCatSolutionPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return "Server working directory";
    }
}
