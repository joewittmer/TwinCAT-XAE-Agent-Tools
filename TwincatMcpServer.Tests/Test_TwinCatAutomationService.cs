using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;
using System.Xml;
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

    [Test]
    public async Task Test_SetTargetNetId_PreservesProjectAttributesWhenXaeDropsThem()
    {
        string projectPath = CreateTwinCatProjectFile();

        try
        {
            FakeSysManager sysManager = new(
                "169.254.162.243.1.1",
                projectPath: projectPath,
                dropTarget64BitOnSet: true);

            using TwinCatAutomationService service = new(Options.Create(new TwinCatAutomationOptions()));
            SetPrivateField(service, "_activeProject", new FakeProject("PlcProgram", sysManager, projectPath));
            SetPrivateField(service, "_sysManager", sysManager);

            object result = await service.SetTargetNetIdAsync("127.0.0.1.1.1");
            string json = JsonSerializer.Serialize(result);
            string projectXml = File.ReadAllText(projectPath);

            Assert.That(json, Does.Contain("\"targetNetId\":\"127.0.0.1.1.1\""));
            Assert.That(json, Does.Contain("\"restoredProjectAttributes\":[\"Target64Bit\"]"));
            Assert.That(projectXml, Does.Contain("TargetNetId=\"127.0.0.1.1.1\""));
            Assert.That(projectXml, Does.Contain("Target64Bit=\"true\""));
            Assert.That(projectXml, Does.Contain("ShowHideConfigurations=\"#x346\""));
        }
        finally
        {
            File.Delete(projectPath);
        }
    }

    [Test]
    public async Task Test_SetTargetNetId_RestoresActiveTargetPlatformWhenXaeChangesIt()
    {
        FakeConfigurationManager configurationManager = new("TwinCAT RT (x64)");
        FakeSysManager sysManager = new(
            "169.254.162.243.1.1",
            configurationManager: configurationManager,
            targetPlatformAfterSet: "TwinCAT RT (ARM)");

        using TwinCatAutomationService service = new(Options.Create(new TwinCatAutomationOptions()));
        SetPrivateField(service, "_activeProject", new FakeProject("PlcProgram", sysManager));
        SetPrivateField(service, "_sysManager", sysManager);

        object result = await service.SetTargetNetIdAsync("127.0.0.1.1.1");
        string json = JsonSerializer.Serialize(result);

        Assert.That(configurationManager.ActiveTargetPlatform, Is.EqualTo("TwinCAT RT (x64)"));
        Assert.That(json, Does.Contain("\"activeTargetPlatform\":\"TwinCAT RT (x64)\""));
        Assert.That(json, Does.Contain("\"activeTargetPlatformRestored\":true"));
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

    private static string CreateTwinCatProjectFile()
    {
        string projectPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{Guid.NewGuid():N}.tsproj");

        File.WriteAllText(
            projectPath,
            """
            <?xml version="1.0"?>
            <TcSmProject TcSmVersion="1.0" TcVersion="3.1.4026.22">
                <DataTypes />
                <Project ProjectGUID="{CD044946-C819-4323-AA89-C9E2DD87FE00}" TargetNetId="169.254.162.243.1.1" Target64Bit="true" ShowHideConfigurations="#x346">
                    <System />
                </Project>
            </TcSmProject>
            """);

        return projectPath;
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

    private sealed class FakeProject(string name, object projectObject, string? fullName = null)
    {
        public string Name { get; } = name;

        public string FullName { get; } = fullName ?? $"C:\\Projects\\Machine\\{name}.tsproj";

        public object Object { get; } = projectObject;
    }

    private sealed class FakeConfigurationManager(string activeTargetPlatform)
    {
        public string ActiveTargetPlatform { get; set; } = activeTargetPlatform;
    }

    private sealed class FakeSysManager
    {
        private readonly string? _projectPath;
        private readonly bool _dropTarget64BitOnSet;
        private readonly string? _targetPlatformAfterSet;
        private string _targetNetId;

        public FakeSysManager(
            string targetNetId,
            string? projectPath = null,
            bool dropTarget64BitOnSet = false,
            FakeConfigurationManager? configurationManager = null,
            string? targetPlatformAfterSet = null)
        {
            _targetNetId = targetNetId;
            _projectPath = projectPath;
            _dropTarget64BitOnSet = dropTarget64BitOnSet;
            ConfigurationManager = configurationManager;
            _targetPlatformAfterSet = targetPlatformAfterSet;
        }

        public FakeConfigurationManager? ConfigurationManager { get; }

        public string GetTargetNetId()
        {
            return _targetNetId;
        }

        public void SetTargetNetId(string netId)
        {
            _targetNetId = netId;

            if (ConfigurationManager is not null &&
                !string.IsNullOrWhiteSpace(_targetPlatformAfterSet))
            {
                ConfigurationManager.ActiveTargetPlatform = _targetPlatformAfterSet;
            }

            if (!string.IsNullOrWhiteSpace(_projectPath))
            {
                RewriteProjectFile(netId);
            }
        }

        public string GetLastErrorMessages()
        {
            return string.Empty;
        }

        private void RewriteProjectFile(string netId)
        {
            XmlDocument document = new()
            {
                PreserveWhitespace = true
            };

            document.Load(_projectPath!);
            XmlElement projectElement = FindSystemManagerProjectElement(document)
                ?? throw new InvalidOperationException("Test project file does not contain a system-manager Project element.");

            projectElement.SetAttribute("TargetNetId", netId);
            if (_dropTarget64BitOnSet)
            {
                projectElement.RemoveAttribute("Target64Bit");
            }

            document.Save(_projectPath!);
        }

        private static XmlElement? FindSystemManagerProjectElement(XmlDocument document)
        {
            XmlElement? root = document.DocumentElement;
            if (root is null)
            {
                return null;
            }

            foreach (XmlNode child in root.ChildNodes)
            {
                if (child is XmlElement element &&
                    string.Equals(element.Name, "Project", StringComparison.Ordinal) &&
                    element.HasAttribute("ProjectGUID"))
                {
                    return element;
                }
            }

            return null;
        }
    }
}
