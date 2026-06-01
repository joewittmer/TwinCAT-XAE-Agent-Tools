# TwinCAT XAE Agent Tools

TwinCAT XAE Agent Tools provides agents develop and build TwinCAT code on a XAE workstation through a MCP client connection. It runs on the Windows engineering workstation with TwinCAT XAE, where it exposes the configured project workspace and selected XAE actions through MCP. The agent can run on that same engineering workstation or connect from another workstation when LAN access is enabled.

Default MCP endpoint:

```text
http://127.0.0.1:5001/mcp
```

## Why Use It?

TwinCAT development often depends on a specific Windows workstation: project files, XAE state, build output, target settings, and System Manager data. This server gives an agent controlled access to that context.

TwinCAT XAE Agent Tools gives the agent a bounded way to:

- Discover and edit TwinCAT project files under the configured workspace.
- Read Structured Text, TwinCAT XML, solution files, and C# helper code.
- Open or attach to TwinCAT XAE.
- Select and build the active TwinCAT project.
- Inspect the TwinCAT System Manager tree.
- Export and import TwinCAT XML through XAE.
- Read or set the target AMS Net ID.
- Switch the active TwinCAT target between Config and Run modes with ADS verification.
- Activate the configuration or restart TwinCAT only after explicit confirmation.

This does not replace TwinCAT XAE. It gives an agent a controlled bridge to the engineering environment so it can help develop and build TwinCAT code locally or through a remote MCP connection.

## Common Uses

- Ask Codex to explain a TwinCAT solution or find where logic is implemented.
- Search POUs, GVLs, DUTs, XML, and helper code from one MCP connection.
- Make reviewed edits inside the configured workspace.
- Build the XAE solution and inspect recent TwinCAT errors.
- Switch TwinCAT into Config mode before controlled system changes, or back to Run mode after activation.
- Export TwinCAT tree item XML for review or controlled modification.
- Work from another computer while XAE stays on the engineering workstation.

## Requirements

- Windows
- TwinCAT XAE installed
- Codex or another MCP client
- A TwinCAT Visual Studio solution (`.sln`) containing a TwinCAT project (`.tsproj`)
- The TwinCAT XAE Agent Tools installer

## Start The Server

1. Install TwinCAT XAE Agent Tools.
2. Start `TwinCAT XAE Agent Tools` from the Windows Start menu.
3. Open the TwinCAT XAE Agent Tools icon in the system tray.
4. Open `Settings`.
5. Click `Browse` next to `Solution`.
6. Choose the TwinCAT `.sln` file.
7. In `Advanced`, leave `Allow LAN access` unchecked unless another machine must reach this server.
8. Click `Save`.
9. On the `General` tab, click `Enable`.
10. Confirm the status changes to `Running`.

The default MCP URL is:

```text
http://127.0.0.1:5001/mcp
```

## Set Up Agent

Open the TwinCAT XAE Agent Tools tray settings, then open `Agents`. The app shows the current setup instructions for this workstation, including local and LAN connection options.

Use `Codex` or `Claude` for client-specific setup. Use `Other` for MCP clients that support Streamable HTTP, such as opencode, Agent Zero, or other agent tools.

## Remote Access

Run TwinCAT XAE Agent Tools on the Windows machine that has TwinCAT XAE installed. A remote agent or MCP client can connect to that machine when LAN access is enabled.

1. In tray settings, open `Advanced` and enable `Allow LAN access`.
2. On the `General` tab, click `Enable`.
3. Configure the client to use:

```text
http://{IP_ADDRESS}:5001/mcp
```

Only enable LAN access on trusted networks. The TwinCAT automation still runs on the Windows machine hosting the server.

## Safety

The server is local-only by default. Leave LAN access disabled unless you intentionally want another machine on the network to reach the MCP server.

Workspace tools are bounded to the configured workspace root.

These TwinCAT tools require explicit `confirm: true` before they run:

- `twincat_consume_xml`
- `twincat_activate_configuration`
- `twincat_restart_runtime`
- `twincat_set_runtime_state`

`twincat_set_runtime_state` reports success only after ADS verification confirms the requested System Service state on the active target. The tool reads ADS state from `AmsPort.SystemService` / port `10000`; it does not rely on the XAE command returning successfully as proof that the target changed mode.

During runtime mode switching, the server may confirm known XAE modal dialogs that match the requested direction. It confirms `Restart TwinCAT System in Config Mode` for Config requests and `Load I/O Devices` for Run requests. It does not automatically confirm `Activate Free Run`, because Free Run is not PLC Run. Unknown modal dialogs are reported with their title, text, and detected button labels.

## Current Limits

- It does not currently format Structured Text.
- It does not expose `dotnet format`, because that formats .NET code and is not suitable for TwinCAT Structured Text.
- It does not yet provide symbol-aware rename for C# or TwinCAT Structured Text.
- It does not currently parse TwinCAT build errors into structured file/line diagnostics.
- It does not allow arbitrary filesystem access outside the configured workspace root.

## Tool Reference

### Workspace Tools

These tools work with files under the workspace root. They are useful even when the MCP client has no separate filesystem connector.

| Tool | Purpose |
| --- | --- |
| `workspace_info` | Show the active workspace root, read limits, search limits, and excluded folders. |
| `workspace_list_files` | Discover files with wildcard filters such as `*.cs`, `**/*.TcPOU`, or `**/*.xml`. |
| `workspace_get_file_info` | Read metadata and SHA-256 for a file or directory. |
| `workspace_read_file` | Read a whole text file or a bounded line range. |
| `workspace_search_text` | Search text files with literal or regex matching. |
| `workspace_write_file` | Create or overwrite a UTF-8 text file inside the workspace. |
| `workspace_replace_text` | Make exact text replacements with an optional replacement-count guard. |

Workspace tools skip common generated folders during discovery and search: `.git`, `.vs`, `.idea`, `bin`, `obj`, `node_modules`, `packages`, and `TestResults`.

### TwinCAT And XAE Tools

These tools work through TcXaeShell and the TwinCAT Automation Interface.

| Tool | Purpose |
| --- | --- |
| `twincat_config` | Show static TwinCAT XAE Agent Tools settings. |
| `xae_status` | Show live XAE connection, solution, active project, target Net ID, and last error state. |
| `xae_attach_or_launch` | Attach to running XAE or launch it. |
| `xae_open_solution` | Open the configured or supplied `.sln` file in XAE. |
| `xae_set_active_project` | Select which TwinCAT project in the solution should be automated. |
| `xae_save_solution` | Save the active TwinCAT project and Visual Studio solution. |
| `xae_close_solution` | Close the open XAE solution. |
| `xae_quit` | Quit TcXaeShell. |
| `xae_build_solution` | Build the open XAE solution. |
| `twincat_lookup_tree_item` | Look up a TwinCAT tree item and optionally include children or XML. |
| `twincat_produce_xml` | Export `ProduceXml` for a TwinCAT tree item. |
| `twincat_consume_xml` | Import XML into a TwinCAT tree item. Requires confirmation. |
| `twincat_create_child` | Call `CreateChild` on a TwinCAT tree item. |
| `twincat_get_target_net_id` | Read the current target AMS Net ID. |
| `twincat_set_target_net_id` | Set the target AMS Net ID. |
| `twincat_activate_configuration` | Activate the loaded TwinCAT configuration. Requires confirmation. |
| `twincat_restart_runtime` | Restart TwinCAT and switch runtime to Run mode. Requires confirmation. |
| `twincat_get_last_error_messages` | Read `ITcSysManager.GetLastErrorMessages`. |

### ADS Tools

These tools use ADS directly or use ADS as the final source of truth.

| Tool | Purpose |
| --- | --- |
| `twincat_set_runtime_state` | Switch to Config or Run mode, handle known XAE dialogs, and verify the final ADS System Service state. Requires confirmation. |

## Developer Guide

### Projects

- `TwincatMcpServer`: ASP.NET Core MCP server using Streamable HTTP.
- `TwincatMcp.Tray`: Avalonia system tray app that starts and stops the server.
- `TwincatMcpServer.Tests`: NUnit tests for pure behavior.

### Design Notes

- Workspace tools operate on files under the configured workspace root.
- TwinCAT/XAE tools operate through TcXaeShell and the TwinCAT Automation Interface COM APIs.
- `workspace_read_file` reads files on disk. `twincat_produce_xml` exports the live TwinCAT tree representation from XAE.
- `workspace_write_file` and `workspace_replace_text` edit files. They do not automatically activate, download, or restart TwinCAT.
- `twincat_consume_xml` changes the open TwinCAT project through XAE. It is more powerful than a text edit and requires confirmation.
- `twincat_set_runtime_state` intentionally overlaps with `twincat_restart_runtime` for Run mode. Use `twincat_set_runtime_state` when the final ADS System Service state must be verified.
- ADS writes are not used for Config mode switching because some targets report `Service is not supported by server` for System Service state writes. XAE performs the switch; ADS verifies the final state.
- `twincat_lookup_tree_item` is for tree metadata and child discovery. `twincat_produce_xml` is the dedicated XML export tool; use `includeXml` on lookup only when one combined response is useful.

### Server Configuration

The server reads `McpConfig` from `appsettings.json` and environment variables.

```json
{
  "McpConfig": {
    "Port": 5001,
    "BindAddress": "127.0.0.1",
    "XaeProgId": "TcXaeShell.DTE.17.0",
    "TwinCatSolutionPath": "C:\\Projects\\MyMachine\\MyMachine.sln",
    "ProjectLoadTimeoutSeconds": 30,
    "WorkspaceRoot": "C:\\Projects\\MyMachine",
    "WorkspaceMaxReadBytes": 1048576,
    "WorkspaceMaxSearchFileBytes": 1048576
  }
}
```

`WorkspaceRoot` is optional. When it is blank, the server uses the directory that contains `TwinCatSolutionPath`. If neither value is set, it uses the server process working directory.

Set `WorkspaceExcludedDirectories` in configuration to replace the default excluded folder list.

The tray app saves user settings to:

```text
%APPDATA%\TwincatMcp\traysettings.json
```

### Run From Source

Run the server directly:

```powershell
dotnet run --project TwincatMcpServer.csproj
```

Run the tray app:

```powershell
dotnet run --project TwincatMcp.Tray/TwincatMcp.Tray.csproj
```

Health check:

```text
http://127.0.0.1:5001/health
```

### Build And Test

```powershell
dotnet build twincat_mcp.sln --configuration Release
dotnet test twincat_mcp.sln --configuration Release
```

Unit tests cover configuration defaults and binding, workspace path safety, file edits, MCP tool metadata, and confirmation guards. Real XAE COM automation is an integration/manual test because it requires TwinCAT XAE on Windows.

### Installer Pipeline

Pushes to `main` run the `Build installer` GitHub Actions workflow. The workflow builds and tests the solution, publishes the tray app as a self-contained single-file `win-x64` executable named `TwinCAT XAE Agent Tools.exe`, builds the Inno Setup installer, and uploads the installer executable as the `twincat-xae-agent-tools-installer` workflow artifact.

Pushing a version tag also creates or updates a GitHub release with the installer attached:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Version information is centralized in `Directory.Build.props`. Bump `Version`, `AssemblyVersion`, and `FileVersion` there before building a release; the installer filename uses the same `Version` value.

### Future Features

These are good next steps for deeper project development:

- Structured build diagnostics with errors, warnings, file paths, and line numbers.
- Project summary tools for PLC projects, POUs, GVLs, DUTs, tasks, I/O devices, and mappings.
- Friendly creation tools for POUs, GVLs, DUTs, tasks, and common I/O nodes.
- Safer XML edit planning that previews `ProduceXml` diffs before `ConsumeXml`.
- TwinCAT-aware rename for PLC objects and Structured Text references.
- Symbol-aware rename for C# using Roslyn workspace APIs.
- Structured Text formatting after choosing a parser-backed formatter that works for TwinCAT syntax.

The MCP C# SDK supplies the server transport and tool registration used here. The workspace capabilities are implemented locally so they can share this server's workspace boundary and TwinCAT solution configuration.

## References

- [Codex MCP configuration](https://developers.openai.com/codex/mcp)
- [MCP C# SDK overview](https://modelcontextprotocol.github.io/csharp-sdk/index.html)
- [MCP C# SDK HTTP transport](https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html)
- [TwinCAT Automation Interface guide](https://mccluretc.github.io/AutomationInterface/)
- [Beckhoff Automation Interface manual PDF](https://download.beckhoff.com/download/document/automation/twincat3/TC3_Automation_Interface_EN.pdf)
- [Beckhoff ITcSysManager](https://infosys.beckhoff.com/content/1033/tc3_automationinterface/242753675.html)
- [Beckhoff tree item browsing](https://infosys.beckhoff.com/content/1033/tc3_automationinterface/242723339.html)
- [Beckhoff ITcSmTreeItem](https://infosys.beckhoff.com/content/1033/tc3_automationinterface/242779659.html)
- [Beckhoff custom TreeItem XML parameters](https://infosys.beckhoff.com/content/1033/tc3_automationinterface/242724875.html)
- [Beckhoff ITcSmTreeItem ConsumeXml](https://infosys.beckhoff.com/content/1033/tc3_automationinterface/242834315.html)
- [Beckhoff TwinCAT ADS .NET](https://www.nuget.org/packages/Beckhoff.TwinCAT.Ads/)
