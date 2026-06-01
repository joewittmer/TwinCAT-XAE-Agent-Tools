using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace TwincatMcpServer.TwinCat;

internal sealed class TwinSafeLoaderService
{
    private const int DefaultCommunicationTimeoutMilliseconds = 10_000;
    private const int DefaultProcessTimeoutMilliseconds = 120_000;
    private const int MaxReturnedOutputLength = 16_000;
    private static readonly Regex DownloadedProjectCrcRegex = new(
        @"Download of '.+'\s+\((?<crc>0x[0-9a-fA-F]+)\)\s+to .+ completed",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly TwinCatAutomationOptions _options;

    public TwinSafeLoaderService(IOptions<TwinCatAutomationOptions> options)
    {
        _options = options.Value;
    }

    public async Task<object> ListLogicDevicesAsync(
        string gateway,
        string? ams,
        string? localAms,
        string? loaderPath,
        int timeoutMilliseconds,
        int processTimeoutMilliseconds)
    {
        string listPath = Path.Combine(Path.GetTempPath(), $"twinsafe_loader_devices_{Guid.NewGuid():N}.csv");
        List<string> arguments = BuildCommunicationArguments(gateway, ams, localAms, timeoutMilliseconds);
        arguments.Add("--list");
        arguments.Add(listPath);

        try
        {
            TwinSafeLoaderRunResult result = await RunLoaderAsync(arguments, loaderPath, processTimeoutMilliseconds);
            string deviceList = File.Exists(listPath) ? await File.ReadAllTextAsync(listPath) : string.Empty;

            return new
            {
                listed = true,
                command = result.RedactedCommand,
                result.ExitCode,
                stdout = Truncate(result.Stdout, MaxReturnedOutputLength),
                stderr = Truncate(result.Stderr, MaxReturnedOutputLength),
                devices = TwinSafeLogicDevice.ParseList(deviceList)
            };
        }
        finally
        {
            TryDeleteFile(listPath);
        }
    }

    public async Task<object> LoadProjectAsync(
        string gateway,
        string? ams,
        string? localAms,
        string user,
        string password,
        string slave,
        string projectPath,
        string? loaderPath,
        int timeoutMilliseconds,
        int processTimeoutMilliseconds,
        bool confirm)
    {
        TwinCatSafety.RequireConfirmation(confirm, "load a TwinSAFE safety project onto a logic component");

        string fullProjectPath = ResolveExistingFile(projectPath, "TwinSAFE project binary");
        List<string> arguments = BuildAuthenticatedArguments(gateway, ams, localAms, user, password, timeoutMilliseconds);
        AddRequiredOption(arguments, "--slave", slave, "EtherCAT slave address", nameof(slave));
        arguments.Add("--proj");
        arguments.Add(fullProjectPath);

        TwinSafeLoaderRunResult result = await RunLoaderAsync(arguments, loaderPath, processTimeoutMilliseconds);

        return new
        {
            loaded = true,
            slave = slave.Trim(),
            projectPath = fullProjectPath,
            projectCrc = TryGetDownloadedProjectCrc(result.Stdout),
            command = result.RedactedCommand,
            result.ExitCode,
            stdout = Truncate(result.Stdout, MaxReturnedOutputLength),
            stderr = Truncate(result.Stderr, MaxReturnedOutputLength)
        };
    }

    public async Task<object> ActivateProjectAsync(
        string gateway,
        string? ams,
        string? localAms,
        string user,
        string password,
        string slave,
        string projectPath,
        string crc,
        string? loaderPath,
        int timeoutMilliseconds,
        int processTimeoutMilliseconds,
        bool confirm)
    {
        TwinCatSafety.RequireConfirmation(confirm, "activate a TwinSAFE safety project on a logic component");

        string fullProjectPath = ResolveExistingFile(projectPath, "TwinSAFE project binary");
        string normalizedCrc = NormalizeProjectCrc(crc);
        List<string> arguments = BuildAuthenticatedArguments(gateway, ams, localAms, user, password, timeoutMilliseconds);
        AddRequiredOption(arguments, "--slave", slave, "EtherCAT slave address", nameof(slave));
        arguments.Add("--proj");
        arguments.Add(fullProjectPath);
        arguments.Add("--crc");
        arguments.Add(normalizedCrc);

        TwinSafeLoaderRunResult result = await RunLoaderAsync(arguments, loaderPath, processTimeoutMilliseconds);

        return new
        {
            activated = true,
            slave = slave.Trim(),
            projectPath = fullProjectPath,
            projectCrc = normalizedCrc,
            command = result.RedactedCommand,
            result.ExitCode,
            stdout = Truncate(result.Stdout, MaxReturnedOutputLength),
            stderr = Truncate(result.Stderr, MaxReturnedOutputLength)
        };
    }

    internal static string NormalizeProjectCrc(string crc)
    {
        if (string.IsNullOrWhiteSpace(crc))
        {
            throw new ArgumentException("Project CRC is required.", nameof(crc));
        }

        string normalized = crc.Trim();
        if (!normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "0x" + normalized;
        }

        if (!Regex.IsMatch(normalized, @"^0x[0-9a-fA-F]+$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("Project CRC must be hexadecimal, for example 0x2d63.", nameof(crc));
        }

        return "0x" + normalized[2..].ToLowerInvariant();
    }

    internal static IReadOnlyList<string> RedactArguments(IReadOnlyList<string> arguments)
    {
        List<string> redacted = new(arguments.Count);

        for (int index = 0; index < arguments.Count; index++)
        {
            redacted.Add(arguments[index]);
            if (string.Equals(arguments[index], "--pass", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < arguments.Count)
            {
                index++;
                redacted.Add("<redacted>");
            }
        }

        return redacted;
    }

    private async Task<TwinSafeLoaderRunResult> RunLoaderAsync(
        IReadOnlyList<string> arguments,
        string? loaderPath,
        int processTimeoutMilliseconds)
    {
        string fileName = ResolveLoaderPath(loaderPath);
        int timeout = NormalizeProcessTimeout(processTimeoutMilliseconds);
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new()
        {
            StartInfo = startInfo
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start TwinSAFE Loader.");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Could not start TwinSAFE Loader '{fileName}'. Set McpConfig:TwinSafeLoaderPath or pass loaderPath.",
                ex);
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(timeout));
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException ex)
        {
            TryKill(process);
            throw new TimeoutException($"TwinSAFE Loader did not finish within {timeout} ms.", ex);
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        IReadOnlyList<string> redactedArguments = RedactArguments(arguments);
        string redactedCommand = FormatCommand(fileName, redactedArguments);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"TwinSAFE Loader exited with code {process.ExitCode}. {FormatOutputForError(stdout, stderr)}");
        }

        return new TwinSafeLoaderRunResult(process.ExitCode, stdout, stderr, redactedCommand);
    }

    private string ResolveLoaderPath(string? loaderPath)
    {
        string selectedPath = FirstNotBlank(loaderPath, _options.TwinSafeLoaderPath, "TwinSAFE_Loader.exe")!;
        bool includesDirectory = !string.IsNullOrWhiteSpace(Path.GetDirectoryName(selectedPath));

        if (!Path.IsPathFullyQualified(selectedPath) && includesDirectory)
        {
            selectedPath = Path.GetFullPath(selectedPath);
        }

        if (includesDirectory && !File.Exists(selectedPath))
        {
            throw new FileNotFoundException($"TwinSAFE Loader was not found: {selectedPath}", selectedPath);
        }

        return selectedPath;
    }

    private static List<string> BuildAuthenticatedArguments(
        string gateway,
        string? ams,
        string? localAms,
        string user,
        string password,
        int timeoutMilliseconds)
    {
        List<string> arguments = BuildCommunicationArguments(gateway, ams, localAms, timeoutMilliseconds);
        AddRequiredOption(arguments, "--user", user, "TwinSAFE user", nameof(user));
        AddRequiredOption(arguments, "--pass", password, "TwinSAFE password", nameof(password));
        return arguments;
    }

    private static List<string> BuildCommunicationArguments(
        string gateway,
        string? ams,
        string? localAms,
        int timeoutMilliseconds)
    {
        List<string> arguments = [];
        AddRequiredOption(arguments, "--gw", gateway, "gateway", nameof(gateway));
        AddOptionalOption(arguments, "--ams", ams);
        AddOptionalOption(arguments, "--localams", localAms);

        int timeout = NormalizeCommunicationTimeout(timeoutMilliseconds);
        arguments.Add("--timeout");
        arguments.Add(timeout.ToString(CultureInfo.InvariantCulture));
        return arguments;
    }

    private static void AddRequiredOption(
        List<string> arguments,
        string name,
        string? value,
        string description,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{description} is required.", parameterName);
        }

        arguments.Add(name);
        arguments.Add(value.Trim());
    }

    private static void AddOptionalOption(List<string> arguments, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        arguments.Add(name);
        arguments.Add(value.Trim());
    }

    private static string ResolveExistingFile(string path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"{description} path is required.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"{description} file not found: {fullPath}", fullPath);
        }

        return fullPath;
    }

    private static int NormalizeCommunicationTimeout(int timeoutMilliseconds)
    {
        return Math.Clamp(
            timeoutMilliseconds <= 0 ? DefaultCommunicationTimeoutMilliseconds : timeoutMilliseconds,
            1,
            600_000);
    }

    private static int NormalizeProcessTimeout(int timeoutMilliseconds)
    {
        return Math.Clamp(
            timeoutMilliseconds <= 0 ? DefaultProcessTimeoutMilliseconds : timeoutMilliseconds,
            1_000,
            3_600_000);
    }

    private static string? TryGetDownloadedProjectCrc(string output)
    {
        Match match = DownloadedProjectCrcRegex.Match(output);
        return match.Success ? NormalizeProjectCrc(match.Groups["crc"].Value) : null;
    }

    private static string FormatCommand(string fileName, IReadOnlyList<string> arguments)
    {
        return string.Join(" ", new[] { fileName }.Concat(arguments.Select(QuoteArgument)));
    }

    private static string QuoteArgument(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        return value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;
    }

    private static string FormatOutputForError(string stdout, string stderr)
    {
        string output = FirstNotBlank(stderr, stdout) ?? "No output was returned.";
        return Truncate(output.Trim(), 2_000);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? FirstNotBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record TwinSafeLoaderRunResult(
        int ExitCode,
        string Stdout,
        string Stderr,
        string RedactedCommand);
}

internal sealed record TwinSafeLogicDevice(
    string EtherCatAddress,
    string FsoeAddress,
    string Type,
    string ProjectCrc,
    string Name,
    string SerialNumber)
{
    public static IReadOnlyList<TwinSafeLogicDevice> ParseList(string deviceList)
    {
        List<TwinSafeLogicDevice> devices = [];

        foreach (string line in SplitLines(deviceList))
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("Upload:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("EtherCAT address;", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] parts = line.Split(';');
            if (parts.Length < 6)
            {
                continue;
            }

            devices.Add(new TwinSafeLogicDevice(
                parts[0].Trim(),
                parts[1].Trim(),
                parts[2].Trim(),
                parts[3].Trim(),
                parts[4].Trim(),
                parts[5].Trim()));
        }

        return devices;
    }

    private static IEnumerable<string> SplitLines(string value)
    {
        using StringReader reader = new(value);
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }
}
