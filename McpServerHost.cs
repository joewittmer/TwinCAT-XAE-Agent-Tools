using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using TwincatMcpServer.Mcp;
using TwincatMcpServer.TwinCat;
using TwincatMcpServer.Workspace;

namespace TwincatMcpServer;

public sealed class McpServerHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private bool _isStarted;

    private McpServerHost(WebApplication app)
    {
        _app = app;
    }

    public bool IsStarted => _isStarted;

    public static McpServerHost Create(
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        string[]? args = null)
    {
        return new McpServerHost(BuildApplication(args ?? [], configurationOverrides));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isStarted)
        {
            return;
        }

        await _app.StartAsync(cancellationToken);
        _isStarted = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isStarted)
        {
            return;
        }

        await _app.StopAsync(cancellationToken);
        _isStarted = false;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _app.DisposeAsync();
    }

    internal static WebApplication BuildApplication(
        string[] args,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        if (configurationOverrides is not null)
        {
            builder.Configuration.AddInMemoryCollection(configurationOverrides);
        }

        builder.Services.Configure<TwinCatAutomationOptions>(
            builder.Configuration.GetSection("McpConfig"));
        builder.Services.AddSingleton<TwinCatAutomationService>();
        builder.Services.AddSingleton<TwinSafeLoaderService>();
        builder.Services.AddSingleton<WorkspaceService>();
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .WithTools<TwinCatMcpTools>()
            .WithTools<WorkspaceMcpTools>();

        TwinCatAutomationOptions options = builder.Configuration
            .GetSection("McpConfig")
            .Get<TwinCatAutomationOptions>() ?? new TwinCatAutomationOptions();
        builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.Port}");

        WebApplication app = builder.Build();

        app.MapGet("/health", () => new
        {
            name = "TwinCAT XAE Agent Tools",
            endpoint = "/mcp"
        });
        app.MapMcp("/mcp");

        return app;
    }
}
