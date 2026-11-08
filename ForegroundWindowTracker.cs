using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Glass2k;

internal sealed class ForegroundWindowTracker : IDisposable
{
    private readonly uint ownProcessId = (uint)Environment.ProcessId;
    private readonly NativeMethods.WinEventDelegate callback;
    private IntPtr hook;

    public IntPtr LastExternalWindow { get; private set; }

    public ForegroundWindowTracker()
    {
        callback = OnForegroundChanged;
        hook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground,
            IntPtr.Zero,
            callback,
            0,
            0,
            NativeMethods.WineventOutOfContext);

        if (hook == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        RememberIfExternal(NativeMethods.GetForegroundWindow());
    }

    public void Dispose()
    {
        if (hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(hook);
            hook = IntPtr.Zero;
        }
    }

    private void OnForegroundChanged(
        IntPtr eventHook,
        uint eventType,
        IntPtr window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (objectId == NativeMethods.ObjectIdWindow && childId == NativeMethods.ChildIdSelf)
        {
            RememberIfExternal(window);
        }
    }

    private void RememberIfExternal(IntPtr window)
    {
        if (window == IntPtr.Zero || !NativeMethods.IsWindow(window))
        {
            return;
        }

        var rootWindow = NativeMethods.GetAncestor(window, NativeMethods.GaRoot);
        if (rootWindow == IntPtr.Zero || !NativeMethods.IsWindowVisible(rootWindow))
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(rootWindow, out var processId);
        if (processId != 0 && processId != ownProcessId)
        {
            LastExternalWindow = rootWindow;
        }
    }
}