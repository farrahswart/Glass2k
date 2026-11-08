using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Glass2k;

internal sealed class WindowOpacityService
{
    private const byte FullyOpaque = byte.MaxValue;
    private readonly Dictionary<IntPtr, WindowState> states = new();
    private readonly uint ownProcessId = (uint)Environment.ProcessId;

    public bool TrySetOpacity(IntPtr window, int opacityPercent, out string error)
    {
        error = string.Empty;
        if (opacityPercent is < 0 or > 100)
        {
            error = "Choose an opacity between 0% and 100%.";
            return false;
        }

        if (!TryGetProcessId(window, out var processId) || processId == ownProcessId)
        {
            error = "The previously active window is no longer available.";
            return false;
        }

        if (states.TryGetValue(window, out var existingState) && existingState.ProcessId != processId)
        {
            states.Remove(window);
        }

        if (opacityPercent == 100)
        {
            return !states.TryGetValue(window, out var state) || Restore(window, state, out error);
        }

        if (!NativeMethods.IsWindowVisible(window))
        {
            error = "The active window is not visible.";
            return false;
        }

        if (states.TryGetValue(window, out existingState))
        {
            return ApplyOpacity(window, existingState, ToAlpha(opacityPercent), out error);
        }

        if (!TryCaptureState(window, processId, out var originalState, out error))
        {
            return false;
        }

        if (!originalState.WasLayered)
        {
            if (!TrySetExtendedStyle(window, originalState.OriginalExtendedStyle | NativeMethods.WsExLayered))
            {
                error = GetLastErrorMessage();
                return false;
            }

            if (!RefreshWindowFrame(window))
            {
                TrySetExtendedStyle(window, originalState.OriginalExtendedStyle);
                error = GetLastErrorMessage();
                return false;
            }
        }

        if (!ApplyOpacity(window, originalState, ToAlpha(opacityPercent), out error))
        {
            if (!originalState.WasLayered)
            {
                TrySetExtendedStyle(window, originalState.OriginalExtendedStyle);
                RefreshWindowFrame(window);
            }

            return false;
        }

        states[window] = originalState;
        return true;
    }

    public void RestoreAll()
    {
        foreach (var (window, state) in states.ToArray())
        {
            Restore(window, state, out _);
        }
    }

    private static bool TryGetProcessId(IntPtr window, out uint processId)
    {
        processId = 0;
        return window != IntPtr.Zero
            && NativeMethods.IsWindow(window)
            && NativeMethods.GetWindowThreadProcessId(window, out processId) != 0;
    }

    private static bool TryCaptureState(
        IntPtr window,
        uint processId,
        out WindowState state,
        out string error)
    {
        Marshal.SetLastPInvokeError(0);
        var extendedStyle = NativeMethods.GetWindowLong(window, NativeMethods.GwlpExStyle);
        if (extendedStyle == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            state = default!;
            error = GetLastErrorMessage();
            return false;
        }

        var wasLayered = (extendedStyle & NativeMethods.WsExLayered) != 0;
        uint colorKey = 0;
        byte alpha = FullyOpaque;
        uint flags = 0;
        if (wasLayered && !NativeMethods.GetLayeredWindowAttributes(window, out colorKey, out alpha, out flags))
        {
            state = default!;
            error = "This window uses a display mode that Glass2k cannot safely restore.";
            return false;
        }

        state = new WindowState(processId, extendedStyle, wasLayered, colorKey, alpha, flags);
        error = string.Empty;
        return true;
    }

    private bool Restore(IntPtr window, WindowState state, out string error)
    {
        error = string.Empty;
        if (!TryGetProcessId(window, out var processId) || processId != state.ProcessId)
        {
            states.Remove(window);
            return true;
        }

        bool restored;
        if (state.WasLayered)
        {
            restored = NativeMethods.SetLayeredWindowAttributes(
                window,
                state.OriginalColorKey,
                state.OriginalAlpha,
                state.OriginalFlags);
        }
        else
        {
            restored = TrySetExtendedStyle(window, state.OriginalExtendedStyle)
                && RefreshWindowFrame(window);
        }

        if (restored)
        {
            states.Remove(window);
            return true;
        }

        error = GetLastErrorMessage();
        return false;
    }

    private static bool ApplyOpacity(IntPtr window, WindowState state, byte alpha, out string error)
    {
        var flags = state.OriginalFlags | NativeMethods.LwaAlpha;
        if (NativeMethods.SetLayeredWindowAttributes(window, state.OriginalColorKey, alpha, flags))
        {
            error = string.Empty;
            return true;
        }

        error = GetLastErrorMessage();
        return false;
    }

    private static byte ToAlpha(int opacityPercent)
    {
        return (byte)Math.Round(opacityPercent * FullyOpaque / 100d, MidpointRounding.AwayFromZero);
    }

    private static bool TrySetExtendedStyle(IntPtr window, int style)
    {
        Marshal.SetLastPInvokeError(0);
        var previousStyle = NativeMethods.SetWindowLong(window, NativeMethods.GwlpExStyle, style);
        return previousStyle != 0 || Marshal.GetLastPInvokeError() == 0;
    }

    private static bool RefreshWindowFrame(IntPtr window)
    {
        const uint flags = NativeMethods.SwpNoMove
            | NativeMethods.SwpNoSize
            | NativeMethods.SwpNoZOrder
            | NativeMethods.SwpNoActivate
            | NativeMethods.SwpFrameChanged;

        return NativeMethods.SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, flags);
    }

    private static string GetLastErrorMessage()
    {
        var errorCode = Marshal.GetLastPInvokeError();
        return errorCode == 0
            ? "Windows could not change this window's opacity."
            : new Win32Exception(errorCode).Message;
    }

    private sealed record WindowState(
        uint ProcessId,
        int OriginalExtendedStyle,
        bool WasLayered,
        uint OriginalColorKey,
        byte OriginalAlpha,
        uint OriginalFlags);
}