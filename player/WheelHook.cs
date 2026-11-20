using System.Runtime.InteropServices;
using System.Windows;

namespace TrayTune;

/// <summary>
/// Колесо мыши над виджетом. Обычный <c>MouseWheel</c> в WPF до нас не дойдёт:
/// окно не активируется (<c>WS_EX_NOACTIVATE</c>), а Windows шлёт колесо окну
/// в фокусе. Поэтому ставим низкоуровневый хук и сами проверяем, где курсор.
/// Хук ставится из UI-потока, поэтому его колбэк выполняется в том же потоке.
/// </summary>
internal sealed class WheelHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMouseWheel = 0x020A;

    private readonly LowLevelMouseProc _proc;
    private readonly Func<Point, bool> _isOverWidget;
    private readonly Action<int> _onWheel;
    private IntPtr _hook;

    public WheelHook(Func<Point, bool> isOverWidget, Action<int> onWheel)
    {
        _isOverWidget = isOverWidget;
        _onWheel = onWheel;

        // Делегат держим в поле: если его соберёт GC, хук упадёт.
        _proc = Callback;
        _hook = SetWindowsHookEx(WhMouseLl, _proc, IntPtr.Zero, 0);

        if (_hook == IntPtr.Zero) Log.Write("wheel: хук не установлен");
    }

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam == (IntPtr)WmMouseWheel)
        {
            try
            {
                var data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
                var cursor = new Point(data.Point.X, data.Point.Y);

                if (_isOverWidget(cursor))
                {
                    var delta = (short)((data.MouseData >> 16) & 0xFFFF);
                    if (delta != 0) _onWheel(delta > 0 ? 1 : -1);
                }
            }
            catch
            {
                // Хук живёт в общем message loop — падать здесь нельзя.
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    // ------------------------------------------------------------ Win32

    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public Point32 Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelMouseProc proc, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
}
