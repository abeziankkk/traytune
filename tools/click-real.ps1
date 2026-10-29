# Real mouse click at screen coordinates (SetCursorPos + mouse_event).
param(
    [Parameter(Mandatory = $true)][int]$X,
    [Parameter(Mandatory = $true)][int]$Y,
    [switch]$Right
)

Add-Type -Namespace W -Name M -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
'@

$MOUSEEVENTF_LEFTDOWN  = 0x0002
$MOUSEEVENTF_LEFTUP    = 0x0004
$MOUSEEVENTF_RIGHTDOWN = 0x0008
$MOUSEEVENTF_RIGHTUP   = 0x0010

$down = if ($Right) { $MOUSEEVENTF_RIGHTDOWN } else { $MOUSEEVENTF_LEFTDOWN }
$up   = if ($Right) { $MOUSEEVENTF_RIGHTUP }   else { $MOUSEEVENTF_LEFTUP }

[void][W.M]::SetCursorPos($X, $Y)
Start-Sleep -Milliseconds 120
[W.M]::mouse_event($down, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 80
[W.M]::mouse_event($up, 0, 0, 0, [UIntPtr]::Zero)

$kind = if ($Right) { "right" } else { "left" }
"$kind click at $X,$Y"
