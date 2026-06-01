using Microsoft.Extensions.Options;
using System.Text.Json;
using TwincatMcpServer.TwinCat;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_TwinCatAutomationService
{
    [Test]
    public async Task Test_GetConfig_DoesNotDuplicateLiveStatus()
    {
        using TwinCatAutomationService service = new(Options.Create(new TwinCatAutomationOptions
        {
            XaeProgId = "TcXaeShell.DTE.17.0",
            TwinCatSolutionPath = "C:\\Projects\\Machine\\Machine.sln"
        }));

        object result = await service.GetConfigAsync();
        string json = JsonSerializer.Serialize(result);

        Assert.That(json, Does.Contain("TcXaeShell.DTE.17.0"));
        Assert.That(json, Does.Contain("Machine.sln"));
        Assert.That(json, Does.Not.Contain("connected"));
        Assert.That(json, Does.Not.Contain("solutionOpen"));
        Assert.That(json, Does.Not.Contain("targetNetId"));
    }

    [Test]
    public void Test_SetRuntimeState_WhenNotConfirmed_ThrowsHelpfulMessageBeforeComAccess()
    {
        using TwinCatAutomationService service = new(Options.Create(new TwinCatAutomationOptions()));

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.SetRuntimeStateAsync("Run", confirm: false))!;

        Assert.That(exception.Message, Is.EqualTo("Set confirm=true to switch TwinCAT runtime state to Run."));
    }
}
