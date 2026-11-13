using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Interop;

/// <summary>
/// Registers a window as a shell AppBar so maximised windows don't cover it
/// (the same mechanism the Windows taskbar uses to reserve its strip).
/// </summary>
internal sealed class ShellAppBar(nint hwnd) : IDisposable
{
    private bool _registered;

    public uint Edge { get; private set; } = ABE_TOP;

    /// <summary>Reserves <paramref name="thickness"/> physical pixels along an edge of the monitor bounds.</summary>
    public RECT Reserve(uint edge, RECT monitor, int thickness)
    {
        var data = NewData();
        if (!_registered)
        {
            SHAppBarMessage(ABM_NEW, ref data);
            _registered = true;
        }

        Edge = edge;
        data.uEdge = edge;
        data.rc = monitor;
        switch (edge)
        {
            case ABE_TOP: data.rc.Bottom = monitor.Top + thickness; break;
            case ABE_BOTTOM: data.rc.Top = monitor.Bottom - thickness; break;
            case ABE_LEFT: data.rc.Right = monitor.Left + thickness; break;
            case ABE_RIGHT: data.rc.Left = monitor.Right - thickness; break;
        }

        // Let the shell adjust for other appbars, then re-apply our thickness.
        SHAppBarMessage(ABM_QUERYPOS, ref data);
        switch (edge)
        {
            case ABE_TOP: data.rc.Bottom = data.rc.Top + thickness; break;
            case ABE_BOTTOM: data.rc.Top = data.rc.Bottom - thickness; break;
            case ABE_LEFT: data.rc.Right = data.rc.Left + thickness; break;
            case ABE_RIGHT: data.rc.Left = data.rc.Right - thickness; break;
        }

        SHAppBarMessage(ABM_SETPOS, ref data);
        return data.rc;
    }

    public void Release()
    {
        if (!_registered)
        {
            return;
        }

        var data = NewData();
        SHAppBarMessage(ABM_REMOVE, ref data);
        _registered = false;
    }

    private APPBARDATA NewData() => new()
    {
        cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
        hWnd = hwnd,
    };

    public void Dispose() => Release();
}
