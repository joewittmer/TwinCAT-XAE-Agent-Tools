using System.Diagnostics;
using TwincatMcp.Tray.Models;

namespace TwincatMcp.Tray.Services;

internal sealed class ServerProcessManager : IDisposable
{
    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    public Task StartAsync(TraySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        ServerLaunch launch = ResolveLaunch();
        ProcessStartInfo startInfo = new()
        {
            FileName = launch.FileName,
            Arguments = launch.Arguments,
            WorkingDirectory = launch.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.Environment["McpConfig__Port"] = settings.Port.ToString();
        startInfo.Environment["McpConfig__BindAddress"] = settings.BindAddress;
        startInfo.Environment["McpConfig__XaeProgId"] = settings.XaeProgId;
        startInfo.Environment["McpConfig__TwinCatSolutionPath"] = settings.TwinCatSolutionPath;
        startInfo.Environment["McpConfig__ProjectLoadTimeoutSeconds"] = settings.ProjectLoadTimeoutSeconds.ToString();

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start TwinCAT XAE Agent Tools.");

        _process.EnableRaisingEvents = true;
        _process.OutputDataReceived += (_, _) => { };
        _process.ErrorDataReceived += (_, _) => { };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_process is null)
        {
            return Task.CompletedTask;
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }

        _process.Dispose();
        _process = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private static ServerLaunch ResolveLaunch()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string exePath = Path.Combine(baseDirectory, "TwincatMcpServer.exe");
        if (File.Exists(exePath))
        {
            return FromPath(exePath);
        }

        string dllPath = Path.Combine(baseDirectory, "TwincatMcpServer.dll");
        if (File.Exists(dllPath))
        {
            return FromPath(dllPath);
        }

        string? projectPath = FindServerProject(baseDirectory);
        if (projectPath is not null)
        {
            string workingDirectory = Path.GetDirectoryName(projectPath) ?? baseDirectory;
            return new ServerLaunch(
                "dotnet",
                $"run --project \"{projectPath}\" --configuration Debug",
                workingDirectory);
        }

        throw new FileNotFoundException(
            "Could not find TwincatMcpServer.exe, TwincatMcpServer.dll, or TwincatMcpServer.csproj.");
    }

    private static ServerLaunch FromPath(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Server path was not found: {path}", path);
        }

        string workingDirectory = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
        if (string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ServerLaunch("dotnet", $"\"{path}\"", workingDirectory);
        }

        return new ServerLaunch(path, string.Empty, workingDirectory);
    }

    private static string? FindServerProject(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);

        while (directory is not null)
        {
            string projectPath = Path.Combine(directory.FullName, "TwincatMcpServer.csproj");
            if (File.Exists(projectPath))
            {
                return projectPath;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record ServerLaunch(string FileName, string Arguments, string WorkingDirectory);
}
