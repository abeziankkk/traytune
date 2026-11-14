# Find SHELLDLL_DefView / SysListView32 in the desktop window tree.
Add-Type -Namespace Wd -Name Api -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hwnd, System.Text.StringBuilder sb, int max);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowW(string cls, string name);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string cls, string name);
'@

function Cls([IntPtr]$h) {
    $sb = New-Object System.Text.StringBuilder 256
    [void][Wd.Api]::GetClassNameW($h, $sb, 256)
    $sb.ToString()
}

$top = [Wd.Api]::GetWindow([Wd.Api]::GetDesktopWindow(), 5)
while ($top -ne [IntPtr]::Zero) {
    $cls = Cls $top
    if ($cls -eq "Progman" -or $cls -eq "WorkerW") {
        "TOP 0x{0:X} {1}" -f ([int64]$top), $cls
        $child = [Wd.Api]::GetWindow($top, 5)
        while ($child -ne [IntPtr]::Zero) {
            "    child 0x{0:X} {1}" -f ([int64]$child), (Cls $child)
            if ((Cls $child) -eq "SHELLDLL_DefView") {
                $lv = [Wd.Api]::GetWindow($child, 5)
                while ($lv -ne [IntPtr]::Zero) {
                    "        leaf 0x{0:X} {1}" -f ([int64]$lv), (Cls $lv)
                    $lv = [Wd.Api]::GetWindow($lv, 2)
                }
            }
            $child = [Wd.Api]::GetWindow($child, 2)
        }
    }
    $top = [Wd.Api]::GetWindow($top, 2)
}

"--- direct lookups ---"
"Progman=0x{0:X}" -f ([int64][Wd.Api]::FindWindowW("Progman", $null))
"DefView under Progman=0x{0:X}" -f ([int64][Wd.Api]::FindWindowExW([Wd.Api]::FindWindowW("Progman", $null), [IntPtr]::Zero, "SHELLDLL_DefView", $null))
