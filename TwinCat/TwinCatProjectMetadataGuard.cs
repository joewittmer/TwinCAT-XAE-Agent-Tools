using System.Collections.ObjectModel;
using System.Text;
using System.Xml;

namespace TwincatMcpServer.TwinCat;

internal static class TwinCatProjectMetadataGuard
{
    private const string ProjectElementName = "Project";
    private const string ProjectGuidAttributeName = "ProjectGUID";
    private const string TargetNetIdAttributeName = "TargetNetId";

    public static TwinCatProjectMetadataSnapshot? Capture(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(projectPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        XmlDocument document = LoadDocument(fullPath);
        XmlElement? projectElement = FindSystemManagerProjectElement(document);
        if (projectElement is null)
        {
            return null;
        }

        Dictionary<string, string> attributes = new(StringComparer.Ordinal);
        foreach (XmlAttribute attribute in projectElement.Attributes)
        {
            if (string.Equals(attribute.Name, TargetNetIdAttributeName, StringComparison.Ordinal))
            {
                continue;
            }

            attributes[attribute.Name] = attribute.Value;
        }

        return new TwinCatProjectMetadataSnapshot(
            fullPath,
            new ReadOnlyDictionary<string, string>(attributes));
    }

    public static TwinCatProjectMetadataRestoreResult RestorePreservedAttributes(
        TwinCatProjectMetadataSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.PreservedProjectAttributes.Count == 0)
        {
            return TwinCatProjectMetadataRestoreResult.NotChecked;
        }

        if (!File.Exists(snapshot.ProjectPath))
        {
            throw new FileNotFoundException(
                $"TwinCAT project file was removed before project metadata could be verified: {snapshot.ProjectPath}",
                snapshot.ProjectPath);
        }

        XmlDocument document = LoadDocument(snapshot.ProjectPath);
        XmlElement projectElement = FindSystemManagerProjectElement(document)
            ?? throw new InvalidOperationException(
                $"TwinCAT project file does not contain a system-manager Project element: {snapshot.ProjectPath}");

        List<string> restoredAttributes = new();
        foreach (KeyValuePair<string, string> attribute in snapshot.PreservedProjectAttributes)
        {
            string? currentValue = projectElement.HasAttribute(attribute.Key)
                ? projectElement.GetAttribute(attribute.Key)
                : null;

            if (string.Equals(currentValue, attribute.Value, StringComparison.Ordinal))
            {
                continue;
            }

            projectElement.SetAttribute(attribute.Key, attribute.Value);
            restoredAttributes.Add(attribute.Key);
        }

        if (restoredAttributes.Count > 0)
        {
            SaveDocument(document, snapshot.ProjectPath);
        }

        return new TwinCatProjectMetadataRestoreResult(
            Checked: true,
            snapshot.ProjectPath,
            restoredAttributes);
    }

    private static XmlDocument LoadDocument(string path)
    {
        XmlDocument document = new()
        {
            PreserveWhitespace = true
        };

        document.Load(path);
        return document;
    }

    private static void SaveDocument(XmlDocument document, string path)
    {
        XmlWriterSettings settings = new()
        {
            Encoding = DetectEncoding(path),
            Indent = false,
            NewLineHandling = NewLineHandling.None
        };

        using XmlWriter writer = XmlWriter.Create(path, settings);
        document.Save(writer);
    }

    private static Encoding DetectEncoding(string path)
    {
        byte[] preamble = new byte[4];
        using FileStream stream = File.OpenRead(path);
        int bytesRead = stream.Read(preamble, 0, preamble.Length);

        if (bytesRead >= 3 &&
            preamble[0] == 0xEF &&
            preamble[1] == 0xBB &&
            preamble[2] == 0xBF)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        if (bytesRead >= 2 &&
            preamble[0] == 0xFF &&
            preamble[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (bytesRead >= 2 &&
            preamble[0] == 0xFE &&
            preamble[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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
            if (child is not XmlElement element ||
                !string.Equals(element.Name, ProjectElementName, StringComparison.Ordinal))
            {
                continue;
            }

            if (element.HasAttribute(ProjectGuidAttributeName))
            {
                return element;
            }
        }

        return null;
    }
}

internal sealed record TwinCatProjectMetadataSnapshot(
    string ProjectPath,
    IReadOnlyDictionary<string, string> PreservedProjectAttributes);

internal sealed record TwinCatProjectMetadataRestoreResult(
    bool Checked,
    string? ProjectPath,
    IReadOnlyList<string> RestoredAttributes)
{
    public static TwinCatProjectMetadataRestoreResult NotChecked { get; } =
        new(Checked: false, ProjectPath: null, RestoredAttributes: Array.Empty<string>());
}
