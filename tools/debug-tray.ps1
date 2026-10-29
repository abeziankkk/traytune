# Диагностика: что вообще видно в дереве окон панели задач.
Add-Type -Namespace W2 -Name U2 -MemberDefinition @'
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowW(string cls, string name);
[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hwnd, System.Text.StringBuilder sb, int max);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left, Top, Right, Bottom; }
'@

$tray = [W2.U2]::FindWindowW("Shell_TrayWnd", $null)
"tray=0x{0:X}" -f ([int64]$tray)
if ($tray -eq [IntPtr]::Zero) { exit 1 }

$r = New-Object W2.U2+RECT
[void][W2.U2]::GetWindowRect($tray, [ref]$r)
"rect=$($r.Left),$($r.Top) $(($r.Right-$r.Left))x$(($r.Bottom-$r.Top))"

$c = [W2.U2]::GetWindow($tray, 5)
"first child=0x{0:X}" -f ([int64]$c)

$n = 0
while ($c -ne [IntPtr]::Zero -and $n -lt 40) {
    $sb = New-Object System.Text.StringBuilder 256
    [void][W2.U2]::GetClassNameW($c, $sb, 256)
    $pid_ = 0
    [void][W2.U2]::GetWindowThreadProcessId($c, [ref]$pid_)
    $rr = New-Object W2.U2+RECT
    [void][W2.U2]::GetWindowRect($c, [ref]$rr)
    "  child 0x{0:X} pid={1,-6} {2,-40} {3},{4} {5}x{6}" -f ([int64]$c), $pid_, $sb.ToString(), $rr.Left, $rr.Top, ($rr.Right-$rr.Left), ($rr.Bottom-$rr.Top)
    $c = [W2.U2]::GetWindow($c, 2)
    $n++
}
"children: $n"
