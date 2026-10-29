using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TrayTune;

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
    private static bool _overlay;

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
        var heightPx = Math.Max(24, trayRect.Height - 6);
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

        var hwndStyleBefore = Native.GetStyleValue(hwnd);
        var parentBefore = Native.GetParent(hwnd);
        Native.SetParent(hwnd, tray);
        var setParentError = Marshal.GetLastWin32Error();

        Native.SetChildStyle(hwnd, true);
        var hwndStyleAfter = Native.GetStyleValue(hwnd);

        const uint flags = Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED | Native.SWP_SHOWWINDOW;
        Native.SetWindowPos(hwnd, Native.HWND_TOP, client.X, client.Y, widthPx, heightPx, flags);

        var parentAfter = Native.GetParent(hwnd);
        Log.Write($"attach: hwnd=0x{hwnd:X} tray=0x{tray:X} parentBefore=0x{parentBefore:X} " +
                  $"styleBefore=0x{hwndStyleBefore:X8} styleAfter=0x{hwndStyleAfter:X8} " +
                  $"err={setParentError} parentAfter=0x{parentAfter:X}");

        if (parentAfter != tray)
        {
            Native.SetParent(hwnd, IntPtr.Zero);
            Native.SetChildStyle(hwnd, false);
            info = $"окно не стало дочерним (parent=0x{parentAfter:X}, err={setParentError})";
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
        var heightPx = Math.Max(24, trayRect.Height - 6);
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

        // UI Automation иногда отвечает секундами или подвисает — даём ему таймаут.
        TaskbarLayout.ButtonBlock? block = null;
        try
        {
            var probe = Task.Run(() => TaskbarLayout.GetButtonBlock(tray));
            if (probe.Wait(TimeSpan.FromMilliseconds(400))) block = probe.Result;
        }
        catch
        {
            block = null;
        }

        if (block is { } b)
        {
            // Если виджет упирается в «Пуск» или значки — сдвигаем за них.
            return b.Left < EdgeMargin + widthPx + 16
                ? Math.Max(EdgeMargin, b.Right + EdgeMargin)
                : EdgeMargin;
        }

        // Фолбэк без UIA: не наезжаем хотя бы на кнопку «Пуск».
        var start = Native.FindWindowEx(tray, IntPtr.Zero, "Start", null);
        if (start != IntPtr.Zero && Native.GetWindowRect(start, out var startRect) &&
            startRect.Left < EdgeMargin + widthPx + 16)
        {
            return Math.Max(EdgeMargin, startRect.Right + EdgeMargin);
        }

        return EdgeMargin;
    }

    /// <summary>
    /// Фолбэк, если система не разрешает SetParent (бывает после обновлений и в
    /// защищённых сессиях). Окно остаётся обычным, но ставится ровно на полосу
    /// панели задач и держится поверх — визуально то же самое.
    /// </summary>
    public static bool TryOverlay(Window window, double widthDip, TaskbarPlacement placement,
        out double heightDip, out string info)
    {
        heightDip = 38;
        info = "";

        var hwnd = new WindowInteropHelper(window).Handle;
        var tray = Native.FindWindow(TrayClass, null);
        if (hwnd == IntPtr.Zero || tray == IntPtr.Zero) return false;
        if (!Native.GetWindowRect(tray, out var trayRect) || trayRect.Height <= 0) return false;

        var scale = GetScale(tray);
        var heightPx = Math.Max(24, trayRect.Height - 6);
        var widthPx = Math.Max(160, (int)Math.Round(widthDip * scale));
        heightDip = heightPx / scale;

        var x = ComputeScreenX(tray, trayRect, widthPx, placement, out _leftX);
        var y = trayRect.Top + (trayRect.Height - heightPx) / 2;

        _placement = placement;
        _overlay = true;

        const uint flags = Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, widthPx, heightPx, flags);
        info = "overlay";
        return true;
    }

    /// <summary>Держит окно на полосе панели задач, если панель переехала.</summary>
    public static void RepositionOverlay(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var tray = Native.FindWindow(TrayClass, null);
        if (hwnd == IntPtr.Zero || tray == IntPtr.Zero) return;
        if (!Native.GetWindowRect(tray, out var trayRect) || trayRect.Height <= 0) return;

        var scale = GetScale(tray);
        var heightPx = Math.Max(24, trayRect.Height - 6);
        var widthPx = Math.Max(160, (int)Math.Round(window.Width * scale));
        var x = ComputeScreenX(tray, trayRect, widthPx, _placement, out _leftX);
        var y = trayRect.Top + (trayRect.Height - heightPx) / 2;

        const uint flags = Native.SWP_NOACTIVATE | Native.SWP_NOZORDER;
        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, widthPx, heightPx, flags);
    }

    /// <summary>Кто-то другой перекрасил окно в обычное — значит оверлей больше не нужен.</summary>
    public static void ClearOverlay() => _overlay = false;

    public static bool IsOverlay => _overlay;

    private static int ComputeScreenX(IntPtr tray, Native.RECT trayRect, int widthPx,
        TaskbarPlacement placement, out int leftX)
    {
        leftX = ComputeLeftX(tray, trayRect, widthPx, placement);

        return placement switch
        {
            TaskbarPlacement.Left => trayRect.Left + leftX,
            TaskbarPlacement.Center => trayRect.Left + (trayRect.Width - widthPx) / 2,
            _ => RightEdge(tray, trayRect) - widthPx,
        };
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

