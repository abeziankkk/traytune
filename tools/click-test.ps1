# Проверка клика по встроенному виджету: находим дочернее окно нашего процесса
# внутри Shell_TrayWnd и посылаем WM_LBUTTONDOWN/UP в заданную точку.
param(
    [string]$ProcessName = "TaskbarPlayer",
    [int]$ClientX = 261,
    [int]$ClientY = 19,
    [switch]$List
)

Add-Type -Namespace W -Name U -MemberDefinition @'
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowW(string cls, string name);
[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hwnd, System.Text.StringBuilder sb, int max);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
[DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
[DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);

[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left, Top, Right, Bottom; }
'@

$tray = [W.U]::FindWindowW("Shell_TrayWnd", $null)
if ($tray -eq [IntPtr]::Zero) { throw "Shell_TrayWnd не найден" }

$proc = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
$match = [IntPtr]::Zero

$child = [W.U]::GetWindow($tray, 5)   # GW_CHILD
while ($child -ne [IntPtr]::Zero) {
    $p = 0
    [void][W.U]::GetWindowThreadProcessId($child, [ref]$p)

    if ($List) {
        $sb = New-Object System.Text.StringBuilder 256
        [void][W.U]::GetClassNameW($child, $sb, 256)
        $rr = New-Object W.U+RECT
        [void][W.U]::GetWindowRect($child, [ref]$rr)
        "0x{0:X} pid={1,-6} {2,-45} {3},{4} {5}x{6}" -f ([int64]$child), $p, $sb.ToString(), $rr.Left, $rr.Top, ($rr.Right-$rr.Left), ($rr.Bottom-$rr.Top)
    }

    if ($p -eq $proc.Id -and $match -eq [IntPtr]::Zero) { $match = $child }
    $child = [W.U]::GetWindow($child, 2)   # GW_HWNDNEXT
}

if ($match -eq [IntPtr]::Zero) {
    "окно процесса $ProcessName внутри панели задач не найдено (pid=$($proc.Id))"
    exit 1
}

$r = New-Object W.U+RECT
[void][W.U]::GetWindowRect($match, [ref]$r)
"hwnd=0x{0:X} parent=0x{1:X} inTray={2}" -f ([int64]$match), ([int64][W.U]::GetParent($match)), ([W.U]::GetParent($match) -eq $tray)
"rect={0},{1} {2}x{3}" -f $r.Left, $r.Top, ($r.Right-$r.Left), ($r.Bottom-$r.Top)

$lp = [IntPtr](($ClientY -shl 16) -bor ($ClientX -band 0xFFFF))
[void][W.U]::PostMessageW($match, 0x0201, [IntPtr]1, $lp)
Start-Sleep -Milliseconds 90
[void][W.U]::PostMessageW($match, 0x0202, [IntPtr]0, $lp)
"click sent at $ClientX,$ClientY"
