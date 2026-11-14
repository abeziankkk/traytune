# Who owns Shell_TrayWnd and at what integrity level.
Add-Type -Namespace Tr -Name Api -MemberDefinition @'
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowW(string cls, string name);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
[DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
'@

$tray = [Tr.Api]::FindWindowW("Shell_TrayWnd", $null)
"tray hwnd = 0x{0:X}" -f ([int64]$tray)
$pid_ = 0
[void][Tr.Api]::GetWindowThreadProcessId($tray, [ref]$pid_)
$proc = Get-Process -Id $pid_ -ErrorAction SilentlyContinue
"owner pid = $pid_ ($($proc.ProcessName))"

$r = New-Object Tr.Api+RECT
[void][Tr.Api]::GetWindowRect($tray, [ref]$r)
"rect = $($r.Left),$($r.Top) $(($r.Right-$r.Left))x$(($r.Bottom-$r.Top))"

# All top-level windows of class Shell_TrayWnd (multi-monitor)
$hits = @()
$w = [Tr.Api]::FindWindowW("Shell_TrayWnd", $null)
while ($w -ne [IntPtr]::Zero) {
    $p2 = 0
    [void][Tr.Api]::GetWindowThreadProcessId($w, [ref]$p2)
    $hits += "0x{0:X} pid={1}" -f ([int64]$w), $p2
    $w = [IntPtr]::Zero
}
$hits

"session id: $((Get-Process -Id $PID).SessionId)"
"explorer session: $((Get-Process explorer | Select-Object -First 1).SessionId)"
