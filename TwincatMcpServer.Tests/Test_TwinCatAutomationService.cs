using Microsoft.Extensions.Options;
using System.Reflection;
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

    [Test]
    public async Task Test_GetStatus_WhenProjectCacheIsEmpty_RefreshesLoadedTwinCatProject()
    {
        FakeSysManager sysManager = new("169.254.162.243.1.1");
        FakeDte dte = CreateDte(
            new FakeProject("SupportProject", new object()),
            new FakeProject("PlcProgram", sysManager));

        using TwinCatAutomationService service = new(Options.Create(new TwinCatAutomationOptions()));
        SetPrivateField(service, "_dte", dte);

        object result = await service.GetStatusAsync();
        string json = JsonSerializer.Serialize(result);

        Assert.That(json, Does.Contain("\"activeProject\":\"PlcProgram\""));
        Assert.That(json, Does.Contain("\"sysManagerReady\":true"));
        Assert.That(json, Does.Contain("\"targetNetId\":\"169.254.162.243.1.1\""));
    }

    [Test]
    public async Task Test_GetTargetNetId_WhenProjectCacheIsEmpty_RefreshesBeforeThrowing()
    {
        FakeSysManager sysManager = new("169.254.162.243.1.1");
        FakeDte dte = CreateDte(new FakeProject("PlcProgram", sysManager));

        using TwinCatAutomationService service = new(Options.Create(new TwinCatAutomationOptions
        {
            ProjectLoadTimeoutSeconds = 1
        }));
        SetPrivateField(service, "_dte", dte);

        object result = await service.GetTargetNetIdAsync();
        string json = JsonSerializer.Serialize(result);

        Assert.That(json, Does.Contain("\"targetNetId\":\"169.254.162.243.1.1\""));
    }

    private static FakeDte CreateDte(params FakeProject[] projects)
    {
        return new FakeDte(new FakeSolution(new FakeProjects(projects)));
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field {fieldName} was not found.");

        field.SetValue(target, value);
    }

    private sealed class FakeDte(FakeSolution solution)
    {
        public FakeSolution Solution { get; } = solution;
    }

    private sealed class FakeSolution(FakeProjects projects)
    {
        public bool IsOpen { get; } = true;

        public string FullName { get; } = "C:\\Projects\\Machine\\Machine.sln";

        public FakeProjects Projects { get; } = projects;
    }

    private sealed class FakeProjects(IReadOnlyList<FakeProject> projects)
    {
        public int Count => projects.Count;

        public FakeProject Item(int index)
        {
            return projects[index - 1];
        }
    }

    private sealed class FakeProject(string name, object projectObject)
    {
        public string Name { get; } = name;

        public object Object { get; } = projectObject;
    }

    private sealed class FakeSysManager(string targetNetId)
    {
        public string GetTargetNetId()
        {
            return targetNetId;
        }

        public string GetLastErrorMessages()
        {
            return string.Empty;
        }
    }
}
