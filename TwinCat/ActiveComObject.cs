using System.Runtime.InteropServices;

namespace TwincatMcpServer.TwinCat;

internal static class ActiveComObject
{
    public static bool IsRunning(string progId)
    {
        IntPtr unknown = IntPtr.Zero;

        try
        {
            CLSIDFromProgID(progId, out Guid clsid);
            GetActiveObjectPointer(ref clsid, IntPtr.Zero, out unknown);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            if (unknown != IntPtr.Zero)
            {
                Marshal.Release(unknown);
            }
        }
    }

    public static object GetInstance(string progId)
    {
        CLSIDFromProgID(progId, out Guid clsid);
        GetActiveObject(ref clsid, IntPtr.Zero, out object instance);
        return instance;
    }

    public static void Release(object? instance)
    {
        if (instance is null || !Marshal.IsComObject(instance))
        {
            return;
        }

        try
        {
            Marshal.FinalReleaseComObject(instance);
        }
        catch (ArgumentException)
        {
        }
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    [DllImport("oleaut32.dll", PreserveSig = false, EntryPoint = "GetActiveObject")]
    private static extern void GetActiveObjectPointer(
        ref Guid rclsid,
        IntPtr pvReserved,
        out IntPtr ppunk);
}
