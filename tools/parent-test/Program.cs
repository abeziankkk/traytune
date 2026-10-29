using System.Runtime.InteropServices;

// Диагностика: почему Shell_TrayWnd не принимает окно как дочернее.
// Запуск: ParentTest.exe [pmv2|system|none]
//   pmv2  — как у нашего WPF-приложения (per-monitor v2)
//   system— system DPI aware
//   none  — DPI unaware

const uint WS_POPUP = 0x80000000;
const uint WS_CHILD = 0x40000000;
const uint WS_VISIBLE = 0x10000000;
const uint WS_EX_LAYERED = 0x00080000;
const uint WS_EX_TOOLWINDOW = 0x00000080;
const int GWL_STYLE = -16;
const uint SWP_NOACTIVATE = 0x0010;
const uint SWP_FRAMECHANGED = 0x0020;
const uint SWP_SHOWWINDOW = 0x0040;

var awareness = args.FirstOrDefault()?.ToLowerInvariant() ?? "pmv2";

var dpiUnaware = new IntPtr(-1);
var dpiSystem = new IntPtr(-2);
var dpiPerMonitorV2 = new IntPtr(-4);

var ctx = awareness switch
{
    "none" => dpiUnaware,
    "system" => dpiSystem,
    _ => dpiPerMonitorV2,
};
SetProcessDpiAwarenessContext(ctx);
Console.WriteLine($"awareness: {awareness} (set ok = {SetProcessDpiAwarenessContext(ctx)})");

var tray = FindWindowW("Shell_TrayWnd", null);
Console.WriteLine($"Shell_TrayWnd = 0x{tray:X}");
if (tray == IntPtr.Zero) return 1;

var wndProc = new WndProcDelegate(DefWindowProcW);
var wc = new WNDCLASS
{
    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
    hInstance = GetModuleHandleW(null),
    lpszClassName = "ParentTestWindow",
};
RegisterClassW(ref wc);

// Кандидаты в родители: сама панель и её заметные дети.
var parents = new List<(string Name, IntPtr Handle)> { ("Shell_TrayWnd", tray) };

foreach (var cls in new[] { "TrayNotifyWnd", "MSTaskSwWClass", "ReBarWindow32", "Windows.UI.Composition.DesktopWindowContentBridge" })
{
    var child = FindWindowExW(tray, IntPtr.Zero, cls, null);
    if (child != IntPtr.Zero) parents.Add((cls, child));
}

foreach (var (name, handle) in parents)
{
    var hwnd = CreateWindowExW(WS_EX_LAYERED | WS_EX_TOOLWINDOW, "ParentTestWindow", "test",
        WS_POPUP | WS_VISIBLE, 100, 100, 200, 40, IntPtr.Zero, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);

    if (hwnd == IntPtr.Zero)
    {
        Console.WriteLine($"{name,-52} create FAIL err={Marshal.GetLastWin32Error()}");
        continue;
    }

    Marshal.SetLastPInvokeError(0);
    SetParent(hwnd, handle);
    var err = Marshal.GetLastPInvokeError();
    var parentRaw = GetParent(hwnd);

    // Ключевой момент: SetParent не меняет стиль. Пока окно осталось WS_POPUP,
    // GetParent возвращает владельца (NULL), а не родителя. Делаем его настоящим child.
    var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
    style = (style & ~WS_POPUP) | WS_CHILD | WS_VISIBLE;
    SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style));
    SetWindowPos(hwnd, IntPtr.Zero, 8, 4, 200, 40, SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);

    var parent = GetParent(hwnd);
    var ok = parent == handle;

    Console.WriteLine($"{name,-52} err={err,-5} rawParent=0x{parentRaw:X} childParent=0x{parent:X} => {(ok ? "OK" : "FAIL")}");

    if (ok) SetParent(hwnd, IntPtr.Zero);
    DestroyWindow(hwnd);
}

return 0;

[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern IntPtr FindWindowExW(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style,
    int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern ushort RegisterClassW(ref WNDCLASS wc);

[DllImport("user32.dll", SetLastError = true)]
static extern IntPtr SetParent(IntPtr child, IntPtr parent);

[DllImport("user32.dll")]
static extern IntPtr GetParent(IntPtr hwnd);

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
static extern IntPtr GetModuleHandleW(string? name);

[DllImport("user32.dll", SetLastError = true)]
static extern bool DestroyWindow(IntPtr hwnd);

[DllImport("user32.dll", SetLastError = true)]
static extern bool SetProcessDpiAwarenessContext(IntPtr value);

[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

[DllImport("user32.dll", SetLastError = true)]
static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct WNDCLASS
{
    public uint style;
    public IntPtr lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public IntPtr hInstance;
    public IntPtr hIcon;
    public IntPtr hCursor;
    public IntPtr hbrBackground;
    public string? lpszMenuName;
    public string lpszClassName;
}
