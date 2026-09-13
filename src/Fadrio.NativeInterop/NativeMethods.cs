using System.Runtime.InteropServices;

namespace Fadrio.NativeInterop;

internal static class NativeMethods
{
    internal const uint ExpectedAbiVersion = 1;
    private const string LibraryName = "fadrio_native";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void EventCallback(IntPtr nativeEvent, IntPtr userData);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SessionInfo
    {
        internal uint Size;
        internal uint NodeId;
        internal int ProcessId;
        internal float Volume;
        internal byte Muted;
        internal byte Active;
        internal IntPtr ApplicationName;
        internal IntPtr ApplicationId;
        internal IntPtr ApplicationIconName;
        internal IntPtr ProcessBinary;
        internal IntPtr MediaName;
        internal IntPtr MediaRole;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Event
    {
        internal uint Size;
        internal NativeEventType Type;
        internal ulong Generation;
        internal SessionInfo Session;
        internal IntPtr Message;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint vm_get_abi_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr vm_context_create(EventCallback callback, IntPtr userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void vm_context_destroy(IntPtr context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int vm_start(IntPtr context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void vm_stop(IntPtr context);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int vm_set_stream_volume(IntPtr context, uint nodeId, float volume);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int vm_set_stream_mute(IntPtr context, uint nodeId, byte muted);
}
