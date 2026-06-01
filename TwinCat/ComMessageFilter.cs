using System.Runtime.InteropServices;

namespace TwincatMcpServer.TwinCat;

internal sealed class ComMessageFilter : IOleMessageFilter, IDisposable
{
    private const int ServerCallHandled = 0;
    private const int ServerCallRejected = 1;
    private const int ServerCallRetryLater = 2;
    private const int RetryDelayMilliseconds = 250;
    private const int MaxRetryMilliseconds = 10_000;

    private IOleMessageFilter? _previousFilter;
    private bool _disposed;

    private ComMessageFilter()
    {
    }

    public static IDisposable Register()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("COM message filters must be registered on an STA thread.");
        }

        ComMessageFilter filter = new();
        int result = CoRegisterMessageFilter(filter, out IOleMessageFilter? previousFilter);
        Marshal.ThrowExceptionForHR(result);
        filter._previousFilter = previousFilter;

        return filter;
    }

    public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
    {
        return ServerCallHandled;
    }

    public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
    {
        if ((dwRejectType == ServerCallRejected || dwRejectType == ServerCallRetryLater) &&
            dwTickCount < MaxRetryMilliseconds)
        {
            return RetryDelayMilliseconds;
        }

        return -1;
    }

    public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
    {
        return 2;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        int result = CoRegisterMessageFilter(_previousFilter, out _);
        Marshal.ThrowExceptionForHR(result);
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(
        IOleMessageFilter? newFilter,
        out IOleMessageFilter? oldFilter);
}

[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    [PreserveSig]
    int HandleInComingCall(
        int dwCallType,
        IntPtr hTaskCaller,
        int dwTickCount,
        IntPtr lpInterfaceInfo);

    [PreserveSig]
    int RetryRejectedCall(
        IntPtr hTaskCallee,
        int dwTickCount,
        int dwRejectType);

    [PreserveSig]
    int MessagePending(
        IntPtr hTaskCallee,
        int dwTickCount,
        int dwPendingType);
}
