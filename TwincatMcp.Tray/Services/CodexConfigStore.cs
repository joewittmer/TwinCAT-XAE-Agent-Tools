using System.Text;

namespace TwincatMcp.Tray.Services;

internal sealed class CodexConfigStore
{
    private const string ServerSectionName = "mcp_servers.twincat";
    internal const int ToolTimeoutSeconds = 120;

    public CodexConfigStore()
        : this(GetDefaultConfigPath())
    {
    }

    internal CodexConfigStore(string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ConfigPath = configPath;
    }

    public string ConfigPath { get; }

    public void SaveTwinCatServer(string mcpUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mcpUrl);

        string existingConfig = File.Exists(ConfigPath)
            ? File.ReadAllText(ConfigPath)
            : string.Empty;
        string updatedConfig = UpsertTwinCatServerConfig(existingConfig, mcpUrl, ToolTimeoutSeconds);

        string? directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(ConfigPath, updatedConfig, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    internal static string UpsertTwinCatServerConfig(string existingConfig, string mcpUrl, int toolTimeoutSeconds)
    {
        ArgumentNullException.ThrowIfNull(existingConfig);
        ArgumentException.ThrowIfNullOrWhiteSpace(mcpUrl);

        if (toolTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toolTimeoutSeconds), "Tool timeout must be positive.");
        }

        string newLine = DetectNewLine(existingConfig);
        List<string> lines = SplitLines(existingConfig);
        List<string> serverLines = BuildTwinCatServerConfigLines(mcpUrl, toolTimeoutSeconds);

        int sectionStart = FindSectionStart(lines, ServerSectionName);
        if (sectionStart >= 0)
        {
            int sectionEnd = sectionStart + 1;
            while (sectionEnd < lines.Count && !IsTableHeader(lines[sectionEnd]))
            {
                sectionEnd++;
            }

            lines.RemoveRange(sectionStart, sectionEnd - sectionStart);
            lines.InsertRange(sectionStart, serverLines);
            if (sectionStart + serverLines.Count < lines.Count &&
                !string.IsNullOrWhiteSpace(lines[sectionStart + serverLines.Count]))
            {
                lines.Insert(sectionStart + serverLines.Count, string.Empty);
            }
        }
        else
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(serverLines);
        }

        return string.Join(newLine, lines) + newLine;
    }

    internal static string BuildTwinCatServerConfig(string mcpUrl, int toolTimeoutSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mcpUrl);

        if (toolTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toolTimeoutSeconds), "Tool timeout must be positive.");
        }

        return string.Join(Environment.NewLine, BuildTwinCatServerConfigLines(mcpUrl, toolTimeoutSeconds)) +
            Environment.NewLine;
    }

    private static string GetDefaultConfigPath()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            throw new InvalidOperationException("Could not determine the user profile folder.");
        }

        return Path.Combine(userProfile, ".codex", "config.toml");
    }

    private static string DetectNewLine(string text)
    {
        if (text.Contains("\r\n", StringComparison.Ordinal))
        {
            return "\r\n";
        }

        if (text.Contains('\n'))
        {
            return "\n";
        }

        return Environment.NewLine;
    }

    private static List<string> SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return [];
        }

        string normalizedText = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string[] splitLines = normalizedText.Split('\n');
        int lineCount = splitLines.Length;

        if (lineCount > 0 && splitLines[^1].Length == 0)
        {
            lineCount--;
        }

        List<string> lines = new(lineCount);
        for (int index = 0; index < lineCount; index++)
        {
            lines.Add(splitLines[index]);
        }

        return lines;
    }

    private static int FindSectionStart(IReadOnlyList<string> lines, string sectionName)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            if (string.Equals(GetTableName(lines[index]), sectionName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static List<string> BuildTwinCatServerConfigLines(string mcpUrl, int toolTimeoutSeconds)
    {
        return
        [
            $"[{ServerSectionName}]",
            $"url = \"{EscapeTomlString(mcpUrl)}\"",
            $"tool_timeout_sec = {toolTimeoutSeconds}"
        ];
    }

    private static bool IsTableHeader(string line)
    {
        return line.TrimStart().StartsWith("[", StringComparison.Ordinal);
    }

    private static string? GetTableName(string line)
    {
        string trimmedLine = line.Trim();
        if (!trimmedLine.StartsWith("[", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("[[", StringComparison.Ordinal))
        {
            return null;
        }

        int closeBracketIndex = trimmedLine.IndexOf(']');
        if (closeBracketIndex <= 0)
        {
            return null;
        }

        string remainingText = trimmedLine[(closeBracketIndex + 1)..].TrimStart();
        if (remainingText.Length > 0 && !remainingText.StartsWith("#", StringComparison.Ordinal))
        {
            return null;
        }

        return trimmedLine[1..closeBracketIndex].Trim();
    }

    private static string EscapeTomlString(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }
}
