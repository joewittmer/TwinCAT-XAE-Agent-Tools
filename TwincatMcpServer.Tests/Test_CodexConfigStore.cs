using TwincatMcp.Tray.Services;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_CodexConfigStore
{
    [Test]
    public void Test_UpsertTwinCatServerConfig_WhenSectionMissing_AppendsSection()
    {
        string existingConfig = "[profiles.default]\r\nmodel = \"gpt-5\"\r\n";

        string updatedConfig = CodexConfigStore.UpsertTwinCatServerConfig(
            existingConfig,
            "http://127.0.0.1:5001/mcp",
            120);

        Assert.That(updatedConfig, Is.EqualTo(
            "[profiles.default]\r\n" +
            "model = \"gpt-5\"\r\n" +
            "\r\n" +
            "[mcp_servers.twincat]\r\n" +
            "url = \"http://127.0.0.1:5001/mcp\"\r\n" +
            "tool_timeout_sec = 120\r\n"));
    }

    [Test]
    public void Test_UpsertTwinCatServerConfig_WhenSectionExists_ReplacesOnlyTwinCatSection()
    {
        string existingConfig =
            "[mcp_servers.twincat]\n" +
            "url = \"http://127.0.0.1:1234/mcp\"\n" +
            "tool_timeout_sec = 5\n" +
            "\n" +
            "[mcp_servers.other]\n" +
            "url = \"http://127.0.0.1:7000/mcp\"\n";

        string updatedConfig = CodexConfigStore.UpsertTwinCatServerConfig(
            existingConfig,
            "http://127.0.0.1:5002/mcp",
            120);

        Assert.That(updatedConfig, Is.EqualTo(
            "[mcp_servers.twincat]\n" +
            "url = \"http://127.0.0.1:5002/mcp\"\n" +
            "tool_timeout_sec = 120\n" +
            "\n" +
            "[mcp_servers.other]\n" +
            "url = \"http://127.0.0.1:7000/mcp\"\n"));
    }

    [Test]
    public void Test_SaveTwinCatServer_WhenConfigDirectoryMissing_CreatesConfigFile()
    {
        string rootDirectory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"codex-config-{Guid.NewGuid():N}");
        string configPath = Path.Combine(rootDirectory, ".codex", "config.toml");

        try
        {
            CodexConfigStore store = new(configPath);

            store.SaveTwinCatServer("http://127.0.0.1:5001/mcp");

            Assert.That(File.Exists(configPath), Is.True);
            string configText = File.ReadAllText(configPath);
            Assert.That(configText, Does.Contain("[mcp_servers.twincat]"));
            Assert.That(configText, Does.Contain("url = \"http://127.0.0.1:5001/mcp\""));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
    }
}
