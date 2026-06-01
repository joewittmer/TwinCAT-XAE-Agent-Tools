using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using TwincatMcpServer.Mcp;
using TwincatMcpServer.TwinCat;
using TwincatMcpServer.Workspace;

namespace TwincatMcpServer;

internal static class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<TwinCatAutomationOptions>(
            builder.Configuration.GetSection("McpConfig"));
        builder.Services.AddSingleton<TwinCatAutomationService>();
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

        app.Run();
    }
}
