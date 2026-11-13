using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using static SpartanX.App.Interop.NativeMethods;

namespace SpartanX.App.Interop;

/// <summary>Helpers that turn a WinUI window into a borderless, always-on-top shell surface.</summary>
internal static class WindowHelper
{
    public static nint GetHwnd(this Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);

    public static double GetScale(this Window window) => GetDpiForWindow(window.GetHwnd()) / 96.0;

    /// <summary>
    /// Borderless, no caption, hidden from Alt+Tab and the taskbar, never takes focus on click.
    /// </summary>
    public static void MakeShellSurface(Window window, bool topmost = true, bool noActivate = true)
    {
        var appWindow = window.AppWindow;
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = topmost;
        presenter.SetBorderAndTitleBar(false, false);
        appWindow.SetPresenter(presenter);
        appWindow.IsShownInSwitchers = false;

        var hwnd = window.GetHwnd();
        var ex = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_TOOLWINDOW;
        ex &= ~WS_EX_APPWINDOW;
        if (noActivate)
        {
            ex |= WS_EX_NOACTIVATE;
        }

        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)ex);

        // No rounded corners or 1px accent border from DWM: we draw our own shapes.
        var corner = DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        var border = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(uint));
    }

    /// <summary>Moves/resizes in physical pixels and shows without stealing focus.</summary>
    public static void PlaceTopmost(Window window, int x, int y, int width, int height)
    {
        SetWindowPos(window.GetHwnd(), HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public static void ShowNoActivate(Window window) => ShowWindow(window.GetHwnd(), SW_SHOWNA);

    /// <summary>
    /// Restricts hit-testing (and painting) to a rectangle in physical pixels relative to the
    /// window. Everything outside it is click-through. Pass null to clear.
    /// </summary>
    public static void SetHitRegion(Window window, Windows.Graphics.RectInt32? rect)
    {
        var hwnd = window.GetHwnd();
        if (rect is not { } r)
        {
            SetWindowRgn(hwnd, 0, true);
            return;
        }

        // Ownership of the region passes to the system; do not delete it.
        var region = CreateRectRgn(r.X, r.Y, r.X + r.Width, r.Y + r.Height);
        SetWindowRgn(hwnd, region, true);
    }

    public static DisplayArea PrimaryDisplay => DisplayArea.Primary;

    public static void CenterOnPrimary(Window window, int widthDip, int heightDip)
    {
        var area = DisplayArea.Primary.WorkArea;
        var scale = window.GetScale();
        var w = (int)(widthDip * scale);
        var h = (int)(heightDip * scale);
        window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h));
    }

    public static void SetIcon(Window window) =>
        window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "SpartanX.ico"));
}
