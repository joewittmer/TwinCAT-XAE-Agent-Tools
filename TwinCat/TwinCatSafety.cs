namespace TwincatMcpServer.TwinCat;

internal static class TwinCatSafety
{
    public static void RequireConfirmation(bool confirm, string operation)
    {
        if (!confirm)
        {
            throw new InvalidOperationException($"Set confirm=true to {operation}.");
        }
    }
}
