namespace TwincatMcpServer.TwinCat;

internal sealed record TwinSafeProjectImportMode(string Name, int Subtype)
{
    public static readonly TwinSafeProjectImportMode CopyToSolutionDirectory = new("CopyToSolutionDirectory", 0);
    public static readonly TwinSafeProjectImportMode MoveToSolutionDirectory = new("MoveToSolutionDirectory", 1);
    public static readonly TwinSafeProjectImportMode UseOriginalLocation = new("UseOriginalLocation", 2);
}

internal static class TwinSafeProjectImportModeParser
{
    public static TwinSafeProjectImportMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TwinSafeProjectImportMode.CopyToSolutionDirectory;
        }

        string normalized = value.Trim();
        if (string.Equals(normalized, "0", StringComparison.Ordinal) ||
            string.Equals(normalized, "Copy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "CopyToSolutionDirectory", StringComparison.OrdinalIgnoreCase))
        {
            return TwinSafeProjectImportMode.CopyToSolutionDirectory;
        }

        if (string.Equals(normalized, "1", StringComparison.Ordinal) ||
            string.Equals(normalized, "Move", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "MoveToSolutionDirectory", StringComparison.OrdinalIgnoreCase))
        {
            return TwinSafeProjectImportMode.MoveToSolutionDirectory;
        }

        if (string.Equals(normalized, "2", StringComparison.Ordinal) ||
            string.Equals(normalized, "UseOriginal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "UseOriginalLocation", StringComparison.OrdinalIgnoreCase))
        {
            return TwinSafeProjectImportMode.UseOriginalLocation;
        }

        throw new ArgumentException(
            "Import mode must be CopyToSolutionDirectory, MoveToSolutionDirectory, or UseOriginalLocation.",
            nameof(value));
    }
}
