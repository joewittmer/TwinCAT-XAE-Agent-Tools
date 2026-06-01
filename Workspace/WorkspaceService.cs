using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TwincatMcpServer.TwinCat;

namespace TwincatMcpServer.Workspace;

internal sealed class WorkspaceService
{
    private const int MaxReturnedResults = 2000;
    private static readonly string[] DefaultExcludedDirectoryNames =
    [
        ".git",
        ".vs",
        ".idea",
        "bin",
        "obj",
        "node_modules",
        "packages",
        "TestResults"
    ];

    private readonly TwinCatAutomationOptions _options;

    public WorkspaceService(IOptions<TwinCatAutomationOptions> options)
    {
        _options = options.Value;
    }

    public Task<object> GetInfoAsync()
    {
        string root = GetWorkspaceRoot();

        return Task.FromResult<object>(new
        {
            workspaceRoot = root,
            configuredWorkspaceRoot = BlankToNull(_options.WorkspaceRoot),
            configuredSolutionPath = BlankToNull(_options.TwinCatSolutionPath),
            maxReadBytes = _options.WorkspaceMaxReadBytes,
            maxSearchFileBytes = _options.WorkspaceMaxSearchFileBytes,
            excludedDirectories = GetExcludedDirectoryNames()
        });
    }

    public Task<object> ListFilesAsync(
        string? path,
        string? pattern,
        bool recursive,
        bool includeDirectories,
        int maxResults)
    {
        string root = GetWorkspaceRoot();
        string basePath = ResolvePath(path, mustExist: true);

        if (!Directory.Exists(basePath))
        {
            throw new DirectoryNotFoundException($"Workspace directory not found: {ToRelativePath(basePath, root)}");
        }

        int limit = ClampResultLimit(maxResults);
        string effectivePattern = string.IsNullOrWhiteSpace(pattern) ? "*" : NormalizeRelativePath(pattern);
        Regex matcher = CreateWildcardRegex(effectivePattern);
        bool patternIncludesPath = effectivePattern.Contains('/', StringComparison.Ordinal);

        List<object> entries = [];

        foreach (string entry in EnumerateEntries(basePath, recursive, includeDirectories))
        {
            string relativePath = ToRelativePath(entry, root);
            string matchPath = patternIncludesPath ? relativePath : Path.GetFileName(entry);

            if (!matcher.IsMatch(NormalizeRelativePath(matchPath)))
            {
                continue;
            }

            bool isDirectory = Directory.Exists(entry);
            entries.Add(new
            {
                path = relativePath,
                kind = isDirectory ? "directory" : "file",
                sizeBytes = isDirectory ? (long?)null : new FileInfo(entry).Length
            });

            if (entries.Count >= limit)
            {
                break;
            }
        }

        return Task.FromResult<object>(new
        {
            workspaceRoot = root,
            path = ToRelativePath(basePath, root),
            pattern = effectivePattern,
            recursive,
            includeDirectories,
            truncated = entries.Count >= limit,
            entries
        });
    }

    public Task<object> GetFileInfoAsync(string path)
    {
        string root = GetWorkspaceRoot();
        string fullPath = ResolvePath(path, mustExist: true);

        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException($"Workspace path not found: {path}", path);
        }

        if (Directory.Exists(fullPath))
        {
            DirectoryInfo directory = new(fullPath);
            return Task.FromResult<object>(new
            {
                path = ToRelativePath(fullPath, root),
                kind = "directory",
                directory.CreationTimeUtc,
                directory.LastWriteTimeUtc
            });
        }

        FileInfo file = new(fullPath);
        return Task.FromResult<object>(new
        {
            path = ToRelativePath(fullPath, root),
            kind = "file",
            sizeBytes = file.Length,
            file.CreationTimeUtc,
            file.LastWriteTimeUtc,
            sha256 = ComputeSha256(fullPath)
        });
    }

    public Task<object> ReadFileAsync(string path, int startLine, int? lineCount, int maxBytes)
    {
        string root = GetWorkspaceRoot();
        string fullPath = ResolvePath(path, mustExist: true);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Workspace file not found: {path}", path);
        }

        int byteLimit = ClampByteLimit(maxBytes, _options.WorkspaceMaxReadBytes);
        int safeStartLine = Math.Max(1, startLine);

        if (lineCount is null && new FileInfo(fullPath).Length > byteLimit)
        {
            throw new InvalidOperationException(
                $"File is larger than {byteLimit} bytes. Pass startLine and lineCount to read a smaller range.");
        }

        if (lineCount is null)
        {
            using StreamReader reader = new(fullPath, detectEncodingFromByteOrderMarks: true);
            string content = reader.ReadToEnd();
            return Task.FromResult<object>(new
            {
                path = ToRelativePath(fullPath, root),
                content,
                startLine = 1,
                endLine = CountLines(content),
                truncatedByMaxBytes = false,
                encoding = reader.CurrentEncoding.WebName,
                sha256 = ComputeSha256(fullPath)
            });
        }

        int safeLineCount = Math.Clamp(lineCount.Value, 0, MaxReturnedResults);
        StringBuilder builder = new();
        int currentLine = 0;
        int returnedLines = 0;
        bool truncatedByMaxBytes = false;

        foreach (string line in File.ReadLines(fullPath))
        {
            currentLine++;

            if (currentLine < safeStartLine)
            {
                continue;
            }

            if (returnedLines >= safeLineCount)
            {
                break;
            }

            if (Encoding.UTF8.GetByteCount(builder.ToString()) + Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length > byteLimit)
            {
                truncatedByMaxBytes = true;
                break;
            }

            if (returnedLines > 0)
            {
                builder.AppendLine();
            }

            builder.Append(line);
            returnedLines++;
        }

        return Task.FromResult<object>(new
        {
            path = ToRelativePath(fullPath, root),
            content = builder.ToString(),
            startLine = safeStartLine,
            endLine = returnedLines == 0 ? safeStartLine - 1 : safeStartLine + returnedLines - 1,
            truncatedByMaxBytes,
            sha256 = ComputeSha256(fullPath)
        });
    }

    public Task<object> SearchTextAsync(
        string query,
        string? path,
        string? filePattern,
        bool regex,
        bool caseSensitive,
        int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query is required.", nameof(query));
        }

        string root = GetWorkspaceRoot();
        string basePath = ResolvePath(path, mustExist: true);
        int limit = ClampResultLimit(maxResults);
        string effectivePattern = string.IsNullOrWhiteSpace(filePattern) ? "*" : NormalizeRelativePath(filePattern);
        Regex fileMatcher = CreateWildcardRegex(effectivePattern);
        bool patternIncludesPath = effectivePattern.Contains('/', StringComparison.Ordinal);
        Regex? textMatcher = regex
            ? new Regex(query, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2))
            : null;
        StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        List<object> results = [];
        IEnumerable<string> files = File.Exists(basePath)
            ? [basePath]
            : EnumerateEntries(basePath, recursive: true, includeDirectories: false).Where(File.Exists);

        foreach (string file in files)
        {
            string relativePath = ToRelativePath(file, root);
            string matchPath = patternIncludesPath ? relativePath : Path.GetFileName(file);

            if (!fileMatcher.IsMatch(NormalizeRelativePath(matchPath)) || ShouldSkipFileForSearch(file))
            {
                continue;
            }

            int lineNumber = 0;

            try
            {
                foreach (string line in File.ReadLines(file))
                {
                    lineNumber++;
                    int column = -1;
                    string? matchedText = null;

                    if (textMatcher is not null)
                    {
                        Match match = textMatcher.Match(line);
                        if (match.Success)
                        {
                            column = match.Index + 1;
                            matchedText = match.Value;
                        }
                    }
                    else
                    {
                        int index = line.IndexOf(query, comparison);
                        if (index >= 0)
                        {
                            column = index + 1;
                            matchedText = query;
                        }
                    }

                    if (column < 0)
                    {
                        continue;
                    }

                    results.Add(new
                    {
                        path = relativePath,
                        line = lineNumber,
                        column,
                        matchedText,
                        preview = Truncate(line.Trim(), 500)
                    });

                    if (results.Count >= limit)
                    {
                        return Task.FromResult<object>(new
                        {
                            query,
                            regex,
                            caseSensitive,
                            path = ToRelativePath(basePath, root),
                            filePattern = effectivePattern,
                            truncated = true,
                            results
                        });
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
            }
        }

        return Task.FromResult<object>(new
        {
            query,
            regex,
            caseSensitive,
            path = ToRelativePath(basePath, root),
            filePattern = effectivePattern,
            truncated = false,
            results
        });
    }

    public Task<object> WriteFileAsync(
        string path,
        string content,
        bool overwrite,
        string? expectedSha256,
        bool createDirectories)
    {
        string root = GetWorkspaceRoot();
        string fullPath = ResolvePath(path, mustExist: false);
        bool existed = File.Exists(fullPath);

        if (existed && !overwrite)
        {
            throw new IOException("File already exists. Pass overwrite=true to replace it.");
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            if (!existed)
            {
                throw new FileNotFoundException("Cannot check expectedSha256 because the file does not exist.", path);
            }

            string actualSha256 = ComputeSha256(fullPath);
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("File hash does not match expectedSha256.");
            }
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            if (!createDirectories)
            {
                throw new DirectoryNotFoundException($"Directory does not exist: {ToRelativePath(directory, root)}");
            }

            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return Task.FromResult<object>(new
        {
            path = ToRelativePath(fullPath, root),
            created = !existed,
            overwritten = existed,
            sizeBytes = new FileInfo(fullPath).Length,
            sha256 = ComputeSha256(fullPath)
        });
    }

    public Task<object> ReplaceTextAsync(
        string path,
        string oldText,
        string newText,
        bool replaceAll,
        int? expectedReplacements)
    {
        if (string.IsNullOrEmpty(oldText))
        {
            throw new ArgumentException("oldText is required.", nameof(oldText));
        }

        string root = GetWorkspaceRoot();
        string fullPath = ResolvePath(path, mustExist: true);
        string content = File.ReadAllText(fullPath);
        int occurrences = CountOccurrences(content, oldText);

        if (occurrences == 0)
        {
            throw new InvalidOperationException("oldText was not found.");
        }

        if (expectedReplacements is not null && occurrences != expectedReplacements.Value)
        {
            throw new InvalidOperationException(
                $"Expected {expectedReplacements.Value} replacement(s), but found {occurrences} occurrence(s).");
        }

        int replacements = replaceAll ? occurrences : 1;
        string updated = replaceAll
            ? content.Replace(oldText, newText, StringComparison.Ordinal)
            : ReplaceFirst(content, oldText, newText);

        File.WriteAllText(fullPath, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return Task.FromResult<object>(new
        {
            path = ToRelativePath(fullPath, root),
            replacements,
            totalMatches = occurrences,
            sha256 = ComputeSha256(fullPath)
        });
    }

    private string ResolvePath(string? path, bool mustExist)
    {
        string root = GetWorkspaceRoot();
        string candidate = string.IsNullOrWhiteSpace(path)
            ? root
            : Path.IsPathRooted(path)
                ? path
                : Path.Combine(root, path);
        string fullPath = Path.GetFullPath(candidate);

        if (!IsInsideRoot(fullPath, root))
        {
            throw new UnauthorizedAccessException($"Path is outside the configured workspace root: {path}");
        }

        if (mustExist && !File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException($"Workspace path not found: {path}", path);
        }

        return fullPath;
    }

    private string GetWorkspaceRoot()
    {
        string? configuredRoot = BlankToNull(_options.WorkspaceRoot);
        if (configuredRoot is not null)
        {
            return Path.GetFullPath(configuredRoot);
        }

        string? configuredSolution = ResolveConfiguredSolutionPath();
        if (configuredSolution is not null)
        {
            string? directory = Path.GetDirectoryName(configuredSolution);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return Path.GetFullPath(directory);
            }
        }

        return Path.GetFullPath(Directory.GetCurrentDirectory());
    }

    private string? ResolveConfiguredSolutionPath()
    {
        string? solutionPath = BlankToNull(_options.TwinCatSolutionPath);
        if (solutionPath is null)
        {
            return null;
        }

        return Path.GetFullPath(solutionPath);
    }

    private IEnumerable<string> EnumerateEntries(string basePath, bool recursive, bool includeDirectories)
    {
        Queue<string> directories = new();
        directories.Enqueue(basePath);

        while (directories.Count > 0)
        {
            string directory = directories.Dequeue();
            IEnumerable<string> entries;

            try
            {
                entries = Directory.EnumerateFileSystemEntries(directory);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (string entry in entries)
            {
                bool isDirectory = Directory.Exists(entry);
                if (isDirectory && IsExcludedDirectory(entry))
                {
                    continue;
                }

                if (!isDirectory || includeDirectories)
                {
                    yield return entry;
                }

                if (recursive && isDirectory)
                {
                    directories.Enqueue(entry);
                }
            }
        }
    }

    private bool ShouldSkipFileForSearch(string path)
    {
        FileInfo file = new(path);
        if (file.Length > Math.Max(1, _options.WorkspaceMaxSearchFileBytes))
        {
            return true;
        }

        try
        {
            Span<byte> buffer = stackalloc byte[(int)Math.Min(file.Length, 4096)];
            using FileStream stream = File.OpenRead(path);
            int read = stream.Read(buffer);
            return buffer[..read].Contains((byte)0);
        }
        catch (IOException)
        {
            return true;
        }
    }

    private bool IsExcludedDirectory(string path)
    {
        string directoryName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return GetExcludedDirectoryNames().Contains(directoryName, StringComparer.OrdinalIgnoreCase);
    }

    private string[] GetExcludedDirectoryNames()
    {
        string[] configured = _options.WorkspaceExcludedDirectories
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return configured.Length == 0 ? DefaultExcludedDirectoryNames : configured;
    }

    private static bool IsInsideRoot(string path, string root)
    {
        string normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        string normalizedPath = Path.GetFullPath(path);

        return string.Equals(
                normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static string ToRelativePath(string path, string root)
    {
        string relativePath = Path.GetRelativePath(root, path);
        return NormalizeRelativePath(relativePath);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static Regex CreateWildcardRegex(string pattern)
    {
        StringBuilder builder = new("^");
        string normalizedPattern = NormalizeRelativePath(pattern);

        for (int index = 0; index < normalizedPattern.Length; index++)
        {
            char character = normalizedPattern[index];
            if (character == '*')
            {
                bool isDoubleStar = index + 1 < normalizedPattern.Length && normalizedPattern[index + 1] == '*';
                if (isDoubleStar)
                {
                    bool followedBySlash = index + 2 < normalizedPattern.Length && normalizedPattern[index + 2] == '/';
                    builder.Append(followedBySlash ? "(?:.*/)?" : ".*");
                    index += followedBySlash ? 2 : 1;
                }
                else
                {
                    builder.Append("[^/]*");
                }
            }
            else if (character == '?')
            {
                builder.Append("[^/]");
            }
            else
            {
                builder.Append(Regex.Escape(character.ToString()));
            }
        }

        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static int ClampResultLimit(int maxResults)
    {
        return Math.Clamp(maxResults <= 0 ? 100 : maxResults, 1, MaxReturnedResults);
    }

    private static int ClampByteLimit(int requested, int configured)
    {
        int fallback = configured <= 0 ? 1024 * 1024 : configured;
        return Math.Clamp(requested <= 0 ? fallback : requested, 1, Math.Max(1, fallback));
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static int CountLines(string content)
    {
        if (content.Length == 0)
        {
            return 0;
        }

        int count = 1;
        foreach (char character in content)
        {
            if (character == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static int CountOccurrences(string content, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = content.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ReplaceFirst(string content, string oldText, string newText)
    {
        int index = content.IndexOf(oldText, StringComparison.Ordinal);
        return index < 0
            ? content
            : string.Concat(content.AsSpan(0, index), newText, content.AsSpan(index + oldText.Length));
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? BlankToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
