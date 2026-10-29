# Где находится окно процесса: top-level список + состояние.
param([string]$ProcessName = "TaskbarPlayer")

Add-Type -Namespace W3 -Name U3 -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
[DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hwnd, System.Text.StringBuilder sb, int max);
[DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
[DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left, Top, Right, Bottom; }
'@

$proc = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
"pid=$($proc.Id)"

$c = [W3.U3]::GetWindow([W3.U3]::GetDesktopWindow(), 5)
while ($c -ne [IntPtr]::Zero) {
    $p = 0
    [void][W3.U3]::GetWindowThreadProcessId($c, [ref]$p)
    if ($p -eq $proc.Id) {
        $sb = New-Object System.Text.StringBuilder 256
        [void][W3.U3]::GetClassNameW($c, $sb, 256)
        $r = New-Object W3.U3+RECT
        [void][W3.U3]::GetWindowRect($c, [ref]$r)
        $style = [W3.U3]::GetWindowLongPtr($c, -16).ToInt64()
        $ex = [W3.U3]::GetWindowLongPtr($c, -20).ToInt64()
        "top hwnd=0x{0:X} cls={1} visible={2} style=0x{3:X8} ex=0x{4:X8} rect={5},{6} {7}x{8}" -f ([int64]$c), $sb.ToString(), [W3.U3]::IsWindowVisible($c), $style, $ex, $r.Left, $r.Top, ($r.Right-$r.Left), ($r.Bottom-$r.Top)
    }
    $c = [W3.U3]::GetWindow($c, 2)
}
"done"
