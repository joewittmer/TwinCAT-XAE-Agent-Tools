using ModelContextProtocol.Server;
using System.ComponentModel;
using TwincatMcpServer.TwinCat;

namespace TwincatMcpServer.Mcp;

[McpServerToolType]
internal sealed class TwinCatMcpTools
{
    [McpServerTool(Name = "twincat_config", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Return configured TwinCAT XAE Agent Tools settings. Use xae_status for live XAE connection state.")]
    public static Task<object> GetConfig(TwinCatAutomationService twinCat)
    {
        return TwinCatToolCall.RunAsync(twinCat.GetConfigAsync);
    }

    [McpServerTool(Name = "xae_status", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Check whether TcXaeShell is running, connected, and has a TwinCAT project loaded.")]
    public static Task<object> GetXaeStatus(TwinCatAutomationService twinCat)
    {
        return TwinCatToolCall.RunAsync(twinCat.GetStatusAsync);
    }

    [McpServerTool(Name = "xae_attach_or_launch", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Attach to a running TcXaeShell DTE COM object, or launch XAE if it is not running.")]
    public static Task<object> AttachOrLaunchXae(
        TwinCatAutomationService twinCat,
        [Description("Show the TcXaeShell window after attaching or launching.")] bool showWindow = true,
        [Description("Optional .sln path to open after connecting.")] string? solutionPath = null,
        [Description("Optional 1-based TwinCAT project index in the solution.")] int? projectIndex = null,
        [Description("Optional TwinCAT project name in the solution.")] string? projectName = null)
    {
        return TwinCatToolCall.RunAsync(() =>
            twinCat.AttachOrLaunchAsync(showWindow, solutionPath, projectIndex, projectName));
    }

    [McpServerTool(Name = "xae_open_solution", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Open a TwinCAT XAE .sln file and wait until the project Object system manager is ready.")]
    public static Task<object> OpenSolution(
        TwinCatAutomationService twinCat,
        [Description("Optional .sln path. Uses McpConfig:TwinCatSolutionPath when omitted.")] string? path = null,
        [Description("Show the TcXaeShell window.")] bool showWindow = true,
        [Description("Optional 1-based TwinCAT project index in the solution.")] int? projectIndex = null,
        [Description("Optional TwinCAT project name in the solution.")] string? projectName = null)
    {
        return TwinCatToolCall.RunAsync(() =>
            twinCat.OpenSolutionAsync(path, showWindow, projectIndex, projectName));
    }

    [McpServerTool(Name = "xae_set_active_project", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Select the TwinCAT project whose project Object COM interface should be used as ITcSysManager.")]
    public static Task<object> SetActiveProject(
        TwinCatAutomationService twinCat,
        [Description("Optional 1-based TwinCAT project index.")] int? projectIndex = null,
        [Description("Optional TwinCAT project name.")] string? projectName = null)
    {
        return TwinCatToolCall.RunAsync(() =>
            twinCat.SetActiveProjectAsync(projectIndex, projectName));
    }

    [McpServerTool(Name = "xae_save_solution", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Save the active TwinCAT project and its Visual Studio solution.")]
    public static Task<object> SaveSolution(TwinCatAutomationService twinCat)
    {
        return TwinCatToolCall.RunAsync(twinCat.SaveSolutionAsync);
    }

    [McpServerTool(Name = "xae_close_solution", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Close the open XAE solution.")]
    public static Task<object> CloseSolution(
        TwinCatAutomationService twinCat,
        [Description("Save the TwinCAT project and solution before closing.")] bool saveFirst = false)
    {
        return TwinCatToolCall.RunAsync(() => twinCat.CloseSolutionAsync(saveFirst));
    }

    [McpServerTool(Name = "xae_quit", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Quit TcXaeShell through the DTE COM interface.")]
    public static Task<object> QuitXae(
        TwinCatAutomationService twinCat,
        [Description("Save the TwinCAT project and solution before quitting.")] bool saveAll = true)
    {
        return TwinCatToolCall.RunAsync(() => twinCat.QuitAsync(saveAll));
    }

    [McpServerTool(Name = "xae_build_solution", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Build the open XAE solution through EnvDTE SolutionBuild.")]
    public static Task<object> BuildSolution(
        TwinCatAutomationService twinCat,
        [Description("Wait for the build to finish before returning.")] bool waitForBuildToFinish = true,
        [Description("Optional solution build configuration to activate first.")] string? configuration = null)
    {
        return TwinCatToolCall.RunAsync(() =>
            twinCat.BuildSolutionAsync(waitForBuildToFinish, configuration));
    }

    [McpServerTool(Name = "twinsafe_import_project", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Import an existing TwinSAFE safety project or archive under the SAFETY node (TISC).")]
    public static Task<object> ImportTwinSafeProject(
        TwinCatAutomationService twinCat,
        [Description("Path to a TwinSAFE .splcproj project file or .tfzip archive.")] string projectPath,
        [Description("Project name to use when copying or moving into the solution. Ignored for UseOriginalLocation.")] string? projectName = null,
        [Description("Import mode: CopyToSolutionDirectory, MoveToSolutionDirectory, or UseOriginalLocation.")] string importMode = "CopyToSolutionDirectory")
    {
        return TwinCatToolCall.RunAsync(() =>
            twinCat.ImportSafetyProjectAsync(projectPath, projectName, importMode));
    }

    [McpServerTool(Name = "twinsafe_loader_list_devices", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Use TwinSAFE Loader to list available TwinSAFE logic components on an EtherCAT network.")]
    public static Task<object> ListTwinSafeLoaderDevices(
        TwinSafeLoaderService twinSafeLoader,
        [Description("IPv4 address or host name of the EtherCAT Mailbox Gateway or, in AoE mode, the EtherCAT master.")] string gateway,
        [Description("Optional AMS Net ID of the EtherCAT master for AoE mode.")] string? ams = null,
        [Description("Optional local AMS Net ID for AoE mode.")] string? localAms = null,
        [Description("Optional path to TwinSAFE_Loader.exe. Uses McpConfig:TwinSafeLoaderPath or PATH when omitted.")] string? loaderPath = null,
        [Description("TwinSAFE Loader communication timeout in milliseconds.")] int timeoutMilliseconds = 10000,
        [Description("Maximum time to wait for the TwinSAFE Loader process in milliseconds.")] int processTimeoutMilliseconds = 120000)
    {
        return TwinCatToolCall.RunAsync(() =>
            twinSafeLoader.ListLogicDevicesAsync(
                gateway,
                ams,
                localAms,
                loaderPath,
                timeoutMilliseconds,
                processTimeoutMilliseconds));
    }

    [McpServerTool(Name = "twinsafe_loader_load_project", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Use TwinSAFE Loader to load a compiled safety project binary onto a TwinSAFE logic component. Requires confirm=true.")]
    public static Task<object> LoadTwinSafeProject(
        TwinSafeLoaderService twinSafeLoader,
        [Description("IPv4 address or host name of the EtherCAT Mailbox Gateway or, in AoE mode, the EtherCAT master.")] string gateway,
        [Description("TwinSAFE logic component user name.")] string user,
        [Description("TwinSAFE logic component password. Passed to TwinSAFE Loader and redacted from returned command text.")] string password,
        [Description("EtherCAT slave address of the TwinSAFE logic component.")] string slave,
        [Description("Path to the compiled TwinSAFE project binary, usually a .bin file.")] string projectPath,
        [Description("Optional AMS Net ID of the EtherCAT master for AoE mode.")] string? ams = null,
        [Description("Optional local AMS Net ID for AoE mode.")] string? localAms = null,
        [Description("Optional path to TwinSAFE_Loader.exe. Uses McpConfig:TwinSafeLoaderPath or PATH when omitted.")] string? loaderPath = null,
        [Description("TwinSAFE Loader communication timeout in milliseconds.")] int timeoutMilliseconds = 10000,
        [Description("Maximum time to wait for the TwinSAFE Loader process in milliseconds.")] int processTimeoutMilliseconds = 120000,
        [Description("Must be true to load the safety project onto hardware.")] bool confirm = false)
    {
        return TwinCatToolCall.RunAsync(() =>
            twinSafeLoader.LoadProjectAsync(
                gateway,
                ams,
                localAms,
                user,
                password,
                slave,
                projectPath,
                loaderPath,
                timeoutMilliseconds,
                processTimeoutMilliseconds,
                confirm));
    }

    [McpServerTool(Name = "twinsafe_loader_activate_project", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Use TwinSAFE Loader to activate a loaded safety project on a TwinSAFE logic component. Requires confirm=true.")]
    public static Task<object> ActivateTwinSafeProject(
        TwinSafeLoaderService twinSafeLoader,
        [Description("IPv4 address or host name of the EtherCAT Mailbox Gateway or, in AoE mode, the EtherCAT master.")] string gateway,
        [Description("TwinSAFE logic component user name.")] string user,
        [Description("TwinSAFE logic component password. Passed to TwinSAFE Loader and redacted from returned command text.")] string password,
        [Description("EtherCAT slave address of the TwinSAFE logic component.")] string slave,
        [Description("Path to the compiled TwinSAFE project binary, usually a .bin file.")] string projectPath,
        [Description("Expected project CRC to activate, for example 0x2d63.")] string crc,
        [Description("Optional AMS Net ID of the EtherCAT master for AoE mode.")] string? ams = null,
        [Description("Optional local AMS Net ID for AoE mode.")] string? localAms = null,
        [Description("Optional path to TwinSAFE_Loader.exe. Uses McpConfig:TwinSafeLoaderPath or PATH when omitted.")] string? loaderPath = null,
        [Description("TwinSAFE Loader communication timeout in milliseconds.")] int timeoutMilliseconds = 10000,
        [Description("Maximum time to wait for the TwinSAFE Loader process in milliseconds.")] int processTimeoutMilliseconds = 120000,
        [Description("Must be true to activate the safety project on hardware.")] bool confirm = false)
    {
        return TwinCatToolCall.RunAsync(() =>
            twinSafeLoader.ActivateProjectAsync(
                gateway,
                ams,
                localAms,
                user,
                password,
                slave,
                projectPath,
                crc,
                loaderPath,
                timeoutMilliseconds,
                processTimeoutMilliseconds,
                confirm));
    }

    [McpServerTool(Name = "twincat_lookup_tree_item", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Look up a TwinCAT tree item by path such as TIPC, TIID, TISM, or TIPC^Plc^Plc Project^POUs^MAIN.")]
    public static Task<object> LookupTreeItem(
        TwinCatAutomationService twinCat,
        [Description("TwinCAT tree path.")] string path,
        [Description("Include direct child item summaries.")] bool includeChildren = true,
        [Description("Include ProduceXml output for this item.")] bool includeXml = false,
        [Description("Pass true to ProduceXml for recursive XML.")] bool recursiveXml = false,
        [Description("Maximum direct children to return.")] int maxChildren = 100)
    {
        return TwinCatToolCall.RunAsync(() =>
            twinCat.LookupTreeItemAsync(path, includeChildren, includeXml, recursiveXml, maxChildren));
    }

    [McpServerTool(Name = "twincat_produce_xml", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Return ITcSmTreeItem ProduceXml for a TwinCAT tree item.")]
    public static Task<object> ProduceXml(
        TwinCatAutomationService twinCat,
        [Description("TwinCAT tree path.")] string path,
        [Description("Return recursive XML when true.")] bool recursive = false)
    {
        return TwinCatToolCall.RunAsync(() => twinCat.ProduceXmlAsync(path, recursive));
    }

    [McpServerTool(Name = "twincat_consume_xml", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Import XML into a TwinCAT tree item through ITcSmTreeItem ConsumeXml. Requires confirm=true.")]
    public static Task<object> ConsumeXml(
        TwinCatAutomationService twinCat,
        [Description("TwinCAT tree path.")] string path,
        [Description("XML command or configuration fragment to consume.")] string xml,
        [Description("Must be true to import XML into the project.")] bool confirm = false)
    {
        return TwinCatToolCall.RunAsync(() => twinCat.ConsumeXmlAsync(path, xml, confirm));
    }

    [McpServerTool(Name = "twincat_create_child", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Call ITcSmTreeItem CreateChild on a parent tree item.")]
    public static Task<object> CreateChild(
        TwinCatAutomationService twinCat,
        [Description("Parent TwinCAT tree path.")] string parentPath,
        [Description("Name of the new child item.")] string name,
        [Description("TwinCAT item subtype.")] int subtype = 0,
        [Description("Optional sibling name to insert before.")] string? before = null,
        [Description("Optional CreateChild info string.")] string? info = null)
    {
        return TwinCatToolCall.RunAsync(() =>
            twinCat.CreateChildAsync(parentPath, name, subtype, before, info));
    }

    [McpServerTool(Name = "twincat_get_target_net_id", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read the current TwinCAT target AMS Net ID from ITcSysManager.")]
    public static Task<object> GetTargetNetId(TwinCatAutomationService twinCat)
    {
        return TwinCatToolCall.RunAsync(twinCat.GetTargetNetIdAsync);
    }

    [McpServerTool(Name = "twincat_set_target_net_id", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Set the TwinCAT target AMS Net ID through ITcSysManager.")]
    public static Task<object> SetTargetNetId(
        TwinCatAutomationService twinCat,
        [Description("AMS Net ID, for example 127.0.0.1.1.1.")] string netId)
    {
        return TwinCatToolCall.RunAsync(() => twinCat.SetTargetNetIdAsync(netId));
    }

    [McpServerTool(Name = "twincat_activate_configuration", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Activate the loaded TwinCAT configuration on the configured target. Requires confirm=true.")]
    public static Task<object> ActivateConfiguration(
        TwinCatAutomationService twinCat,
        [Description("Must be true to activate the configuration.")] bool confirm = false)
    {
        return TwinCatToolCall.RunAsync(() => twinCat.ActivateConfigurationAsync(confirm));
    }

    [McpServerTool(Name = "twincat_restart_runtime", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Restart TwinCAT and place the runtime in Run mode. Requires confirm=true.")]
    public static Task<object> RestartRuntime(
        TwinCatAutomationService twinCat,
        [Description("Must be true to restart TwinCAT.")] bool confirm = false)
    {
        return TwinCatToolCall.RunAsync(() => twinCat.RestartTwinCatAsync(confirm));
    }

    [McpServerTool(Name = "twincat_set_runtime_state", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Switch the active TwinCAT target to Config or Run mode and verify the final ADS state. Requires confirm=true.")]
    public static Task<object> SetRuntimeState(
        TwinCatAutomationService twinCat,
        [Description("Requested TwinCAT runtime state: Config or Run.")] string state,
        [Description("Must be true to switch the TwinCAT runtime state.")] bool confirm = false)
    {
        return TwinCatToolCall.RunAsync(() => twinCat.SetRuntimeStateAsync(state, confirm));
    }

    [McpServerTool(Name = "twincat_get_last_error_messages", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read ITcSysManager GetLastErrorMessages.")]
    public static Task<object> GetLastErrorMessages(TwinCatAutomationService twinCat)
    {
        return TwinCatToolCall.RunAsync(twinCat.GetLastErrorMessagesAsync);
    }
}
