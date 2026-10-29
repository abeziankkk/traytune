# Bring a window of the given process (optionally the one with a given width) to front.
# ASCII-only: PowerShell 5.1 reads scripts as ANSI.
param(
    [string]$ProcessName = "TaskbarPlayer",
    [int]$Width = 0
)

Add-Type -Namespace Fr -Name Api -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
[DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);

[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left, Top, Right, Bottom; }
'@

$proc = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
$found = [IntPtr]::Zero
$foundRect = New-Object Fr.Api+RECT

$w = [Fr.Api]::GetWindow([Fr.Api]::GetDesktopWindow(), 5)
while ($w -ne [IntPtr]::Zero) {
    $p = 0
    [void][Fr.Api]::GetWindowThreadProcessId($w, [ref]$p)
    if ($p -eq $proc.Id -and [Fr.Api]::IsWindowVisible($w)) {
        $r = New-Object Fr.Api+RECT
        [void][Fr.Api]::GetWindowRect($w, [ref]$r)
        $wpx = $r.Right - $r.Left
        if ($Width -eq 0 -or $wpx -eq $Width) {
            $found = $w
            $foundRect = $r
            if ($Width -ne 0) { break }
        }
    }
    $w = [Fr.Api]::GetWindow($w, 2)
}

if ($found -eq [IntPtr]::Zero) { "window not found"; exit 1 }

[void][Fr.Api]::ShowWindow($found, 9)   # SW_RESTORE
[void][Fr.Api]::BringWindowToTop($found)
[void][Fr.Api]::SetForegroundWindow($found)
"raised hwnd=0x{0:X} rect={1},{2} {3}x{4}" -f ([int64]$found), $foundRect.Left, $foundRect.Top, ($foundRect.Right - $foundRect.Left), ($foundRect.Bottom - $foundRect.Top)
