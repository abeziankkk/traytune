# Помощники для чистых скриншотов:
#   desktop.ps1 -ToggleIcons   — скрыть/показать значки рабочего стола
#   desktop.ps1 -MinimizeAll   — свернуть/развернуть все окна (Win+D)
param(
    [switch]$ToggleIcons,
    [switch]$MinimizeAll
)

Add-Type -Namespace Dsk -Name Api -MemberDefinition @'
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowW(string cls, string name);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string cls, string name);
[DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
'@

if ($ToggleIcons) {
    $WM_COMMAND = 0x0111
    $TOGGLE_DESKTOP_ICONS = 0x7402

    # SHELLDLL_DefView живёт то под Progman, то под одним из WorkerW
    $defView = [IntPtr]::Zero
    $progman = [Dsk.Api]::FindWindowW("Progman", $null)
    if ($progman -ne [IntPtr]::Zero) {
        $defView = [Dsk.Api]::FindWindowExW($progman, [IntPtr]::Zero, "SHELLDLL_DefView", $null)
    }

    if ($defView -eq [IntPtr]::Zero) {
        $worker = [IntPtr]::Zero
        do {
            $worker = [Dsk.Api]::FindWindowExW([IntPtr]::Zero, $worker, "WorkerW", $null)
            if ($worker -ne [IntPtr]::Zero) {
                $defView = [Dsk.Api]::FindWindowExW($worker, [IntPtr]::Zero, "SHELLDLL_DefView", $null)
            }
        } while ($worker -ne [IntPtr]::Zero -and $defView -eq [IntPtr]::Zero)
    }

    if ($defView -eq [IntPtr]::Zero) { throw "SHELLDLL_DefView не найден" }

    $list = [Dsk.Api]::FindWindowExW($defView, [IntPtr]::Zero, "SysListView32", $null)
    if ($list -eq [IntPtr]::Zero) { throw "SysListView32 не найден" }

    [void][Dsk.Api]::SendMessageW($list, $WM_COMMAND, [IntPtr]$TOGGLE_DESKTOP_ICONS, [IntPtr]::Zero)
    "иконки рабочего стола переключены"
}

if ($MinimizeAll) {
    $VK_LWIN = 0x5B
    $VK_D = 0x44
    $KEYUP = 0x0002

    [Dsk.Api]::keybd_event($VK_LWIN, 0, 0, [UIntPtr]::Zero)
    [Dsk.Api]::keybd_event($VK_D, 0, 0, [UIntPtr]::Zero)
    [Dsk.Api]::keybd_event($VK_D, 0, $KEYUP, [UIntPtr]::Zero)
    [Dsk.Api]::keybd_event($VK_LWIN, 0, $KEYUP, [UIntPtr]::Zero)
    "Win+D"
}
