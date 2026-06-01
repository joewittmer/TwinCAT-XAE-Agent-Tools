using Microsoft.Extensions.Configuration;
using TwincatMcpServer.TwinCat;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_TwinCatAutomationOptions
{
    [Test]
    public void Test_Defaults_AreLocalAndMatchCurrentXaeProgId()
    {
        TwinCatAutomationOptions options = new();

        Assert.That(options.Port, Is.EqualTo(5001));
        Assert.That(options.BindAddress, Is.EqualTo("127.0.0.1"));
        Assert.That(options.XaeProgId, Is.EqualTo("TcXaeShell.DTE.17.0"));
        Assert.That(options.ProjectLoadTimeoutSeconds, Is.EqualTo(30));
        Assert.That(options.WorkspaceMaxReadBytes, Is.EqualTo(1024 * 1024));
        Assert.That(options.WorkspaceMaxSearchFileBytes, Is.EqualTo(1024 * 1024));
        Assert.That(options.WorkspaceExcludedDirectories, Is.Empty);
    }

    [Test]
    public void Test_ConfigurationBinding_OverridesExpectedValues()
    {
        Dictionary<string, string?> values = new()
        {
            ["McpConfig:Port"] = "5010",
            ["McpConfig:BindAddress"] = "0.0.0.0",
            ["McpConfig:XaeProgId"] = "TcXaeShell.DTE.16.0",
            ["McpConfig:TwinCatSolutionPath"] = "C:\\Projects\\Machine\\Machine.sln",
            ["McpConfig:ProjectLoadTimeoutSeconds"] = "45",
            ["McpConfig:WorkspaceRoot"] = "C:\\Projects\\Machine",
            ["McpConfig:WorkspaceMaxReadBytes"] = "2000000",
            ["McpConfig:WorkspaceMaxSearchFileBytes"] = "3000000",
            ["McpConfig:WorkspaceExcludedDirectories:0"] = ".git",
            ["McpConfig:WorkspaceExcludedDirectories:1"] = "Build"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        TwinCatAutomationOptions options = configuration
            .GetSection("McpConfig")
            .Get<TwinCatAutomationOptions>()!;

        Assert.That(options.Port, Is.EqualTo(5010));
        Assert.That(options.BindAddress, Is.EqualTo("0.0.0.0"));
        Assert.That(options.XaeProgId, Is.EqualTo("TcXaeShell.DTE.16.0"));
        Assert.That(options.TwinCatSolutionPath, Is.EqualTo("C:\\Projects\\Machine\\Machine.sln"));
        Assert.That(options.ProjectLoadTimeoutSeconds, Is.EqualTo(45));
        Assert.That(options.WorkspaceRoot, Is.EqualTo("C:\\Projects\\Machine"));
        Assert.That(options.WorkspaceMaxReadBytes, Is.EqualTo(2000000));
        Assert.That(options.WorkspaceMaxSearchFileBytes, Is.EqualTo(3000000));
        Assert.That(options.WorkspaceExcludedDirectories, Is.EqualTo(new[] { ".git", "Build" }));
    }
}
