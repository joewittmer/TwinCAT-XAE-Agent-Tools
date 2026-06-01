namespace TwincatMcpServer.TwinCat;

internal sealed class TwinCatAutomationOptions
{
    public int Port { get; set; } = 5001;

    public string BindAddress { get; set; } = "127.0.0.1";

    public string XaeProgId { get; set; } = "TcXaeShell.DTE.17.0";

    public string? TwinCatSolutionPath { get; set; }

    public int ProjectLoadTimeoutSeconds { get; set; } = 30;

    public string? WorkspaceRoot { get; set; }

    public int WorkspaceMaxReadBytes { get; set; } = 1024 * 1024;

    public int WorkspaceMaxSearchFileBytes { get; set; } = 1024 * 1024;

    public string[] WorkspaceExcludedDirectories { get; set; } = [];
}
