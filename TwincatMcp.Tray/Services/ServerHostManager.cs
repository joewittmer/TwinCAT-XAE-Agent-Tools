using TwincatMcp.Tray.Models;
using TwincatMcpServer;

namespace TwincatMcp.Tray.Services;

internal sealed class ServerHostManager : IDisposable
{
    private McpServerHost? _host;

    public bool IsRunning => _host?.IsStarted == true;

    public async Task StartAsync(TraySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (IsRunning)
        {
            return;
        }

        Dictionary<string, string?> configurationOverrides = new()
        {
            ["McpConfig:Port"] = settings.Port.ToString(),
            ["McpConfig:BindAddress"] = settings.BindAddress,
            ["McpConfig:XaeProgId"] = settings.XaeProgId,
            ["McpConfig:TwinCatSolutionPath"] = settings.TwinCatSolutionPath,
            ["McpConfig:ProjectLoadTimeoutSeconds"] = settings.ProjectLoadTimeoutSeconds.ToString()
        };

        _host = McpServerHost.Create(configurationOverrides);

        try
        {
            await _host.StartAsync();
        }
        catch
        {
            await DisposeHostAsync();
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_host is null)
        {
            return;
        }

        await DisposeHostAsync();
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task DisposeHostAsync()
    {
        if (_host is null)
        {
            return;
        }

        try
        {
            await _host.DisposeAsync();
        }
        finally
        {
            _host = null;
        }
    }
}
