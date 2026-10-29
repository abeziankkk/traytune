using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TrayTune;

internal static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;
    public const long WS_EX_NOACTIVATE = 0x08000000;
    public const long WS_EX_TOOLWINDOW = 0x00000080;

    public const uint WS_POPUP = 0x80000000;
    public const uint WS_CHILD = 0x40000000;
    public const uint WS_VISIBLE = 0x10000000;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public static readonly IntPtr HWND_TOP = IntPtr.Zero;
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hWnd);

    /// <summary>Разбирает данные, но нужен для диагностики встраивания.</summary>
    public static long GetStyleValue(IntPtr hwnd) => GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();

    /// <summary>
    /// Убирает окно из Alt-Tab и запрещает ему забирать фокус клавиатуры:
    /// клики работают, а активное приложение не теряет фокус.
    /// </summary>
    public static void MakeNoActivateToolWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        var style = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
        style |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(style));
    }

    /// <summary>
    /// Переключает «не забирать фокус». На время диалога настроек запрет снимается,
    /// иначе окно настроек не получит клавиатуру.
    /// </summary>
    public static void SetActivatable(Window window, bool activatable)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;

        var style = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
        style = activatable ? style & ~WS_EX_NOACTIVATE : style | WS_EX_NOACTIVATE;
        SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(style));
    }

    /// <summary>
    /// Переводит окно между top-level (WS_POPUP) и дочерним (WS_CHILD).
    /// Без этого SetParent формально проходит, но окно остаётся top-level:
    /// GetParent возвращает владельца, и настоящего встраивания не происходит.
    /// </summary>
    public static void SetChildStyle(IntPtr hwnd, bool child)
    {
        var style = (uint)GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        style = child
            ? (style & ~WS_POPUP) | WS_CHILD | WS_VISIBLE
            : (style & ~WS_CHILD) | WS_POPUP | WS_VISIBLE;

        SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr((long)style));
        SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }
}
