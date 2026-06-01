using System.Text.Json;
using TwincatMcp.Tray.Services;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_ClientConfigurationText
{
    [Test]
    public void Test_BuildCodexToml_UsesUrlAndToolTimeout()
    {
        string configText = ClientConfigurationText.BuildCodexToml("http://engineering-pc:5001/mcp");

        Assert.That(configText, Does.Contain("[mcp_servers.twincat]"));
        Assert.That(configText, Does.Contain("url = \"http://engineering-pc:5001/mcp\""));
        Assert.That(configText, Does.Contain("tool_timeout_sec = 120"));
    }

    [Test]
    public void Test_BuildClaudeCodeCommand_UsesHttpTransport()
    {
        string command = ClientConfigurationText.BuildClaudeCodeCommand("http://engineering-pc:5001/mcp");

        Assert.That(command, Is.EqualTo(
            "claude mcp add --transport http twincat http://engineering-pc:5001/mcp"));
    }

    [Test]
    public void Test_BuildClaudeCodeJson_UsesHttpTypeAndTimeoutMilliseconds()
    {
        string json = ClientConfigurationText.BuildClaudeCodeJson("http://engineering-pc:5001/mcp");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement server = document.RootElement
            .GetProperty("mcpServers")
            .GetProperty("twincat");

        Assert.That(server.GetProperty("type").GetString(), Is.EqualTo("http"));
        Assert.That(server.GetProperty("url").GetString(), Is.EqualTo("http://engineering-pc:5001/mcp"));
        Assert.That(server.GetProperty("timeout").GetInt32(), Is.EqualTo(120000));
    }

    [Test]
    public void Test_BuildGenericMcpJson_UsesHttpTypeAndUrl()
    {
        string json = ClientConfigurationText.BuildGenericMcpJson("http://engineering-pc:5001/mcp");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement server = document.RootElement
            .GetProperty("mcpServers")
            .GetProperty("twincat");

        Assert.That(server.GetProperty("type").GetString(), Is.EqualTo("http"));
        Assert.That(server.GetProperty("url").GetString(), Is.EqualTo("http://engineering-pc:5001/mcp"));
    }

    [Test]
    public void Test_BuildOtherClientInstructions_UsesConnectionValues()
    {
        string instructions = ClientConfigurationText.BuildOtherClientInstructions("http://engineering-pc:5001/mcp");

        Assert.That(instructions, Does.Contain("Server name: twincat"));
        Assert.That(instructions, Does.Contain("Transport: Streamable HTTP"));
        Assert.That(instructions, Does.Contain("MCP URL: http://engineering-pc:5001/mcp"));
        Assert.That(instructions, Does.Contain("Suggested tool timeout: 120 seconds"));
    }
}
