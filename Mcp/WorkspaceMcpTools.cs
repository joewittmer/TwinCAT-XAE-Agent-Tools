using ModelContextProtocol.Server;
using System.ComponentModel;
using TwincatMcpServer.Workspace;

namespace TwincatMcpServer.Mcp;

[McpServerToolType]
internal sealed class WorkspaceMcpTools
{
    [McpServerTool(Name = "workspace_info", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Return the configured workspace root, solution path, file limits, and excluded directories.")]
    public static Task<object> GetWorkspaceInfo(WorkspaceService workspace)
    {
        return TwinCatToolCall.RunAsync(workspace.GetInfoAsync);
    }

    [McpServerTool(Name = "workspace_list_files", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List files under the bounded workspace root, with optional wildcard filtering.")]
    public static Task<object> ListFiles(
        WorkspaceService workspace,
        [Description("Optional workspace-relative directory to list. Defaults to the workspace root.")] string? path = null,
        [Description("Wildcard pattern such as *.cs or **/*.TcPOU. Defaults to *.")] string? pattern = null,
        [Description("Search subdirectories when true.")] bool recursive = true,
        [Description("Include matching directories as well as files.")] bool includeDirectories = false,
        [Description("Maximum entries to return.")] int maxResults = 500)
    {
        return TwinCatToolCall.RunAsync(() =>
            workspace.ListFilesAsync(path, pattern, recursive, includeDirectories, maxResults));
    }

    [McpServerTool(Name = "workspace_get_file_info", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Return metadata and SHA-256 for a workspace file or directory.")]
    public static Task<object> GetFileInfo(
        WorkspaceService workspace,
        [Description("Workspace-relative file or directory path.")] string path)
    {
        return TwinCatToolCall.RunAsync(() => workspace.GetFileInfoAsync(path));
    }

    [McpServerTool(Name = "workspace_read_file", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read a UTF-8 or BOM-marked text file from the bounded workspace root.")]
    public static Task<object> ReadFile(
        WorkspaceService workspace,
        [Description("Workspace-relative file path.")] string path,
        [Description("1-based line number to start from when lineCount is provided.")] int startLine = 1,
        [Description("Optional number of lines to read. Omit to read the whole file if it is under the byte limit.")] int? lineCount = null,
        [Description("Maximum bytes to return, capped by server configuration.")] int maxBytes = 131072)
    {
        return TwinCatToolCall.RunAsync(() =>
            workspace.ReadFileAsync(path, startLine, lineCount, maxBytes));
    }

    [McpServerTool(Name = "workspace_search_text", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Search text files in the bounded workspace root and return file, line, column, and preview matches.")]
    public static Task<object> SearchText(
        WorkspaceService workspace,
        [Description("Literal text or regex pattern to search for.")] string query,
        [Description("Optional workspace-relative file or directory path. Defaults to the workspace root.")] string? path = null,
        [Description("Optional wildcard file filter such as *.cs, **/*.TcPOU, or **/*.xml.")] string? filePattern = null,
        [Description("Treat query as a regular expression when true.")] bool regex = false,
        [Description("Use ordinal case-sensitive matching when true.")] bool caseSensitive = false,
        [Description("Maximum matches to return.")] int maxResults = 100)
    {
        return TwinCatToolCall.RunAsync(() =>
            workspace.SearchTextAsync(query, path, filePattern, regex, caseSensitive, maxResults));
    }

    [McpServerTool(Name = "workspace_write_file", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Create or overwrite a text file inside the bounded workspace root.")]
    public static Task<object> WriteFile(
        WorkspaceService workspace,
        [Description("Workspace-relative file path.")] string path,
        [Description("Full file content to write as UTF-8 without BOM.")] string content,
        [Description("Allow replacing an existing file.")] bool overwrite = false,
        [Description("Optional SHA-256 the existing file must match before overwriting.")] string? expectedSha256 = null,
        [Description("Create parent directories when missing.")] bool createDirectories = true)
    {
        return TwinCatToolCall.RunAsync(() =>
            workspace.WriteFileAsync(path, content, overwrite, expectedSha256, createDirectories));
    }

    [McpServerTool(Name = "workspace_replace_text", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Replace exact text in a workspace file. Use expectedReplacements to guard precise edits.")]
    public static Task<object> ReplaceText(
        WorkspaceService workspace,
        [Description("Workspace-relative file path.")] string path,
        [Description("Exact text to replace.")] string oldText,
        [Description("Replacement text.")] string newText,
        [Description("Replace all occurrences instead of just the first match.")] bool replaceAll = false,
        [Description("Optional exact number of matches expected before editing.")] int? expectedReplacements = null)
    {
        return TwinCatToolCall.RunAsync(() =>
            workspace.ReplaceTextAsync(path, oldText, newText, replaceAll, expectedReplacements));
    }
}
