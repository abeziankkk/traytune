using System.Runtime.InteropServices;
using System.Windows;

namespace TrayTune;

/// <summary>
/// Колесо мыши над виджетом через Raw Input. Низкоуровневый хук для этого плохо
/// подходит: Windows выбрасывает событие, если колбэк не успел за ~300 мс, а окно
/// виджета никогда не в фокусе, поэтому обычное <c>MouseWheel</c> до него не доходит.
///
/// Raw Input нельзя зарегистрировать на дочернее окно панели задач, поэтому держим
/// свой невидимый top-level оконный класс и ставим приёмник (<c>RIDEV_INPUTSINK</c>)
/// на него: ввод приходит даже когда приложение не в фокусе.
/// </summary>
internal sealed class RawWheel : IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevRemove = 0x00000001;
    private const ushort RiMouseWheel = 0x0400;
    private const uint WsPopup = 0x80000000;

    private readonly Action<int> _onWheel;
    private readonly Func<Rect> _widgetRect;
    private readonly WndProc _wndProc;
    private readonly string _className;
    private IntPtr _hwnd;
    private ushort _classAtom;
    private bool _registered;

    /// <summary>Прямоугольник виджета в экранных координатах — спрашиваем у окна.</summary>
    internal readonly struct Rect
    {
        public Rect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }
    }

    public RawWheel(Func<Rect?> widgetRect, Action<int> onWheel)
    {
        _onWheel = onWheel;
        _widgetRect = () => widgetRect() ?? default;
        _wndProc = Proc;
        _className = "TrayTuneRawWheel_" + Guid.NewGuid().ToString("N");

        try
        {
            var wc = new WndClassEx
            {
                Size = (uint)Marshal.SizeOf<WndClassEx>(),
                WndProc = _wndProc,
                Instance = GetModuleHandle(null),
                ClassName = _className,
            };

            _classAtom = RegisterClassEx(ref wc);
            if (_classAtom == 0)
            {
                Log.Write("wheel: класс окна не зарегистрирован, код " + Marshal.GetLastWin32Error());
                return;
            }

            _hwnd = CreateWindowEx(0, _className, "TrayTuneWheel", WsPopup, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, wc.Instance, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                Log.Write("wheel: окно приёма не создано, код " + Marshal.GetLastWin32Error());
                return;
            }

            var devices = new[]
            {
                new RawInputDevice
                {
                    UsagePage = 0x01,
                    Usage = 0x02,          // мышь
                    Flags = RidevInputSink,
                    Target = _hwnd,
                },
            };

            _registered = RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>());
            Log.Write(_registered ? "wheel: Raw Input подключён" : "wheel: Raw Input не зарегистрирован");
        }
        catch (Exception e)
        {
            Log.Write("wheel: Raw Input упал: " + e.Message);
        }
    }

    private IntPtr Proc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmInput)
        {
            try
            {
                HandleInput(lParam);
            }
            catch
            {
                // Приёмник ввода не должен падать: он в общем message loop окна.
            }
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void HandleInput(IntPtr rawInput)
    {
        var headerSize = Marshal.SizeOf<RawInputHeader>();
        var size = 0;

        GetRawInputData(rawInput, RidInput, IntPtr.Zero, ref size, (uint)headerSize);
        if (size <= 0) return;

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetRawInputData(rawInput, RidInput, buffer, ref size, (uint)headerSize) != size) return;

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != 0) return;   // 0 = RIM_TYPEMOUSE

            var mouse = Marshal.PtrToStructure<RawMouse>(IntPtr.Add(buffer, headerSize));
            var rect = _widgetRect();
            GetCursorPos(out var cursor);


            if ((mouse.ButtonFlags & RiMouseWheel) == 0) return;
            if (mouse.ButtonData == 0) return;

            var inside = cursor.X >= rect.Left && cursor.X <= rect.Right
                && cursor.Y >= rect.Top && cursor.Y <= rect.Bottom;

            if (inside) _onWheel(mouse.ButtonData > 0 ? 1 : -1);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_registered)
            {
                var devices = new[] { new RawInputDevice { Flags = RidevRemove } };
                RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>());
                _registered = false;
            }

            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }

            if (_classAtom != 0)
            {
                UnregisterClass(_className, GetModuleHandle(null));
                _classAtom = 0;
            }
        }
        catch
        {
            // Закрываемся — мусор в лог не нужен.
        }
    }

    // ------------------------------------------------------------ Win32

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public WndProc WndProc;
        public int ClsExtra;
        public int WndExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouse
    {
        public ushort Flags;
        public ushort Padding;
        public ushort ButtonFlags;
        public short ButtonData;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx wc);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref int size, uint headerSize);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point32 point);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
