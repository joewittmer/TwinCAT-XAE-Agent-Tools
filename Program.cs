namespace TwincatMcpServer;

internal static class Program
{
    public static void Main(string[] args)
    {
        var app = McpServerHost.BuildApplication(args);
        app.Run();
    }
}
