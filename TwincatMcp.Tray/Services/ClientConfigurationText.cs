using System.Text.Json;

namespace TwincatMcp.Tray.Services;

internal static class ClientConfigurationText
{
    private const int ClaudeToolTimeoutMilliseconds = CodexConfigStore.ToolTimeoutSeconds * 1000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string BuildCodexToml(string mcpUrl)
    {
        return CodexConfigStore.BuildTwinCatServerConfig(mcpUrl, CodexConfigStore.ToolTimeoutSeconds);
    }

    public static string BuildRemoteCodexInstructions(string mcpUrl)
    {
        return
            "Create or edit ~/.codex/config.toml." + Environment.NewLine +
            "Paste this section. For LAN access, replace 127.0.0.1 with the XAE engineering workstation IP address:" + Environment.NewLine +
            Environment.NewLine +
            BuildCodexToml(mcpUrl) +
            "Restart Codex." +
            Environment.NewLine;
    }

    public static string BuildClaudeCodeCommand(string mcpUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mcpUrl);

        return $"claude mcp add --transport http twincat {mcpUrl}";
    }

    public static string BuildClaudeCodeJson(string mcpUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mcpUrl);

        Dictionary<string, object> root = new()
        {
            ["mcpServers"] = new Dictionary<string, object>
            {
                ["twincat"] = new Dictionary<string, object>
                {
                    ["type"] = "http",
                    ["url"] = mcpUrl,
                    ["timeout"] = ClaudeToolTimeoutMilliseconds
                }
            }
        };

        return JsonSerializer.Serialize(root, JsonOptions) + Environment.NewLine;
    }

    public static string BuildGenericMcpJson(string mcpUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mcpUrl);

        Dictionary<string, object> root = new()
        {
            ["mcpServers"] = new Dictionary<string, object>
            {
                ["twincat"] = new Dictionary<string, object>
                {
                    ["type"] = "http",
                    ["url"] = mcpUrl
                }
            }
        };

        return JsonSerializer.Serialize(root, JsonOptions) + Environment.NewLine;
    }

    public static string BuildClaudeCodeInstructions(string mcpUrl)
    {
        return
            "Create or edit a project .mcp.json file." + Environment.NewLine +
            "Paste this JSON. For LAN access, replace 127.0.0.1 with the XAE engineering workstation IP address:" + Environment.NewLine +
            Environment.NewLine +
            BuildClaudeCodeJson(mcpUrl) +
            "Restart Claude Code or run /mcp." +
            Environment.NewLine;
    }

    public static string BuildOtherClientInstructions(string mcpUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mcpUrl);

        return
            "Server name: twincat" + Environment.NewLine +
            "Transport: Streamable HTTP" + Environment.NewLine +
            $"MCP URL: {mcpUrl}" + Environment.NewLine +
            $"Suggested tool timeout: {CodexConfigStore.ToolTimeoutSeconds} seconds" + Environment.NewLine +
            Environment.NewLine +
            "For clients that accept mcpServers-style JSON:" + Environment.NewLine +
            Environment.NewLine +
            BuildGenericMcpJson(mcpUrl);
    }
}
