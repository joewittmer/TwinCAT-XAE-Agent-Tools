using System.Text.Json.Serialization;

namespace TwincatMcp.Tray.Models;

internal sealed class TraySettings
{
    public string TwinCatSolutionPath { get; set; } = string.Empty;

    public string XaeProgId { get; set; } = "TcXaeShell.DTE.17.0";

    public int Port { get; set; } = 5001;

    public bool AllowLanAccess { get; set; }

    public int ProjectLoadTimeoutSeconds { get; set; } = 30;

    [JsonIgnore]
    public string BindAddress => AllowLanAccess ? "0.0.0.0" : "127.0.0.1";

    [JsonIgnore]
    public string LocalMcpUrl => $"http://127.0.0.1:{Port}/mcp";
}
