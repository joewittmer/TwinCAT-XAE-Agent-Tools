using ModelContextProtocol;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TwincatMcpServer.Mcp;

internal static class TwinCatToolCall
{
    public static async Task<object> RunAsync(Func<Task<object>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new McpException(FormatException(ex), ex);
        }
    }

    private static string FormatException(Exception exception)
    {
        Exception ex = exception;

        while (ex is TargetInvocationException { InnerException: not null } wrapper)
        {
            ex = wrapper.InnerException!;
        }

        if (ex is COMException comException)
        {
            return $"{comException.Message} (HRESULT 0x{comException.HResult:X8})";
        }

        return string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
    }
}
