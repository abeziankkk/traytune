using System.Windows;
using System.Windows.Interop;

namespace TaskbarPlayer;

/// <summary>
/// Экспериментальный режим: окно становится дочерним для Shell_TrayWnd,
/// то есть физически живёт внутри панели задач.
///
/// Важная деталь: SetParent сам по себе не делает окно дочерним — оно остаётся
/// top-level (WS_POPUP), и GetParent возвращает владельца, а не родителя.
/// Поэтому после SetParent обязательно переключается стиль на WS_CHILD.
///
/// Риск: панель задач принадлежит explorer.exe. При перезапуске explorer
/// дочернее окно погибнет вместе с родителем.
/// </summary>
internal static class TaskbarHost
{
    private const string TrayClass = "Shell_TrayWnd";
    private const string NotifyClass = "TrayNotifyWnd";
    private const int EdgeMargin = 8;

    private static TaskbarPlacement _placement = TaskbarPlacement.Left;
    private static int _leftX = EdgeMargin;

    public static bool TryAttach(Window window, double widthDip, TaskbarPlacement placement,
        out double heightDip, out string info)
    {
        heightDip = 38;
        info = "";

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            info = "окно ещё не создано";
            return false;
        }

        var tray = Native.FindWindow(TrayClass, null);
        if (tray == IntPtr.Zero)
        {
            info = "Shell_TrayWnd не найден";
            return false;
        }

        if (!Native.GetWindowRect(tray, out var trayRect) || trayRect.Height <= 0)
        {
            info = "не удалось прочитать размер панели задач";
            return false;
        }

        var scale = GetScale(tray);

        // Оставляем небольшой воздух сверху и снизу.
        var heightPx = Math.Max(20, trayRect.Height - 10);
        var widthPx = Math.Max(160, (int)Math.Round(widthDip * scale));
        heightDip = heightPx / scale;

        _placement = placement;
        _leftX = ComputeLeftX(tray, trayRect, widthPx, placement);

        var x = placement == TaskbarPlacement.Left
            ? _leftX
            : placement == TaskbarPlacement.Center
                ? (trayRect.Width - widthPx) / 2
                : RightEdge(tray, trayRect) - widthPx;

        var y = trayRect.Top + (trayRect.Height - heightPx) / 2;

        var client = new Native.POINT { X = x, Y = y };
        Native.ScreenToClient(tray, ref client);

        Native.SetParent(hwnd, tray);
        Native.SetChildStyle(hwnd, true);

        const uint flags = Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED | Native.SWP_SHOWWINDOW;
        Native.SetWindowPos(hwnd, Native.HWND_TOP, client.X, client.Y, widthPx, heightPx, flags);

        if (Native.GetParent(hwnd) != tray)
        {
            Native.SetParent(hwnd, IntPtr.Zero);
            Native.SetChildStyle(hwnd, false);
            info = "окно не стало дочерним";
            return false;
        }

        info = $"панель {trayRect.Width}x{trayRect.Height}px, dpi x{scale:0.##}";
        return true;
    }

    /// <summary>Панель задач могла переехать или изменить размер — возвращаем окно на место.</summary>
    public static void Reposition(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var tray = Native.FindWindow(TrayClass, null);
        if (hwnd == IntPtr.Zero || tray == IntPtr.Zero) return;
        if (Native.GetParent(hwnd) != tray) return;

        if (!Native.GetWindowRect(tray, out var trayRect) || trayRect.Height <= 0) return;

        var scale = GetScale(tray);
        var heightPx = Math.Max(20, trayRect.Height - 10);
        var widthPx = Math.Max(160, (int)Math.Round(window.Width * scale));

        var x = _placement switch
        {
            TaskbarPlacement.Left => _leftX,
            TaskbarPlacement.Center => (trayRect.Width - widthPx) / 2,
            _ => RightEdge(tray, trayRect) - widthPx,
        };

        var y = trayRect.Top + (trayRect.Height - heightPx) / 2;

        var client = new Native.POINT { X = x, Y = y };
        Native.ScreenToClient(tray, ref client);

        Native.SetWindowPos(hwnd, Native.HWND_TOP, client.X, client.Y, widthPx, heightPx,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    /// <summary>Смена положения (слева/центр/справа) без пересоздания окна.</summary>
    public static void SetPlacement(Window window, TaskbarPlacement placement)
    {
        _placement = placement;

        var tray = Native.FindWindow(TrayClass, null);
        if (tray == IntPtr.Zero) return;
        if (!Native.GetWindowRect(tray, out var trayRect)) return;

        var scale = GetScale(tray);
        var widthPx = Math.Max(160, (int)Math.Round(window.Width * scale));
        _leftX = ComputeLeftX(tray, trayRect, widthPx, placement);

        Reposition(window);
    }

    public static void Detach(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        if (Native.GetParent(hwnd) == IntPtr.Zero) return;

        Native.SetParent(hwnd, IntPtr.Zero);
        Native.SetChildStyle(hwnd, false);
    }

    public static bool IsAttached(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;

        var tray = Native.FindWindow(TrayClass, null);
        return tray != IntPtr.Zero && Native.GetParent(hwnd) == tray;
    }

    private static int ComputeLeftX(IntPtr tray, Native.RECT trayRect, int widthPx, TaskbarPlacement placement)
    {
        if (placement != TaskbarPlacement.Left) return EdgeMargin;

        // Слева стоит кнопка «Пуск» — если виджет в неё упрётся, сдвигаем за значки приложений.
        var block = TaskbarLayout.GetButtonBlock(tray);
        if (block is { } b && b.Left < EdgeMargin + widthPx + 16)
        {
            return Math.Max(EdgeMargin, b.Right + EdgeMargin);
        }

        return EdgeMargin;
    }

    private static int RightEdge(IntPtr tray, Native.RECT trayRect)
    {
        var right = trayRect.Right - EdgeMargin;
        var notify = Native.FindWindowEx(tray, IntPtr.Zero, NotifyClass, null);
        if (notify != IntPtr.Zero && Native.GetWindowRect(notify, out var notifyRect) && notifyRect.Width > 40)
        {
            right = notifyRect.Left - 6;
        }
        return right;
    }

    private static double GetScale(IntPtr tray)
    {
        var dpi = Native.GetDpiForWindow(tray);
        return dpi <= 0 ? 1.0 : dpi / 96.0;
    }
}
