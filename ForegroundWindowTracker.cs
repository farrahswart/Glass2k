using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Glass2k;

internal sealed class ForegroundWindowTracker : IDisposable
{
    private const int MaximumRememberedWindows = 16;
    private static readonly HashSet<string> ExcludedWindowClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "Progman",
        "WorkerW"
    };
    private readonly uint ownProcessId = (uint)Environment.ProcessId;
    private readonly NativeMethods.WinEventDelegate callback;
    private readonly List<WindowReference> recentWindows = new();
    private IntPtr hook;

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

    public IntPtr GetMostRecentVisibleWindow()
    {
        for (var index = 0; index < recentWindows.Count;)
        {
            var candidate = recentWindows[index];
            if (!NativeMethods.IsWindow(candidate.Handle))
            {
                recentWindows.RemoveAt(index);
                continue;
            }

            var threadId = NativeMethods.GetWindowThreadProcessId(candidate.Handle, out var processId);
            if (threadId == 0
                || processId != candidate.ProcessId
                || processId == ownProcessId
                || IsExcludedWindowsSurface(candidate.Handle))
            {
                recentWindows.RemoveAt(index);
                continue;
            }

            if (NativeMethods.IsWindowVisible(candidate.Handle))
            {
                return candidate.Handle;
            }

            index++;
        }

        return IntPtr.Zero;
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
        if (rootWindow == IntPtr.Zero
            || !NativeMethods.IsWindowVisible(rootWindow)
            || IsExcludedWindowsSurface(rootWindow))
        {
            return;
        }

        NativeMethods.GetWindowThreadProcessId(rootWindow, out var processId);
        if (processId != 0 && processId != ownProcessId)
        {
            recentWindows.RemoveAll(candidate => candidate.Handle == rootWindow);
            recentWindows.Insert(0, new WindowReference(rootWindow, processId));
            if (recentWindows.Count > MaximumRememberedWindows)
            {
                recentWindows.RemoveRange(MaximumRememberedWindows, recentWindows.Count - MaximumRememberedWindows);
            }
        }
    }

    private static bool IsExcludedWindowsSurface(IntPtr window)
    {
        var className = new StringBuilder(256);
        var length = NativeMethods.GetClassName(window, className, className.Capacity);
        return length > 0 && ExcludedWindowClasses.Contains(className.ToString());
    }

    private sealed record WindowReference(IntPtr Handle, uint ProcessId);
}