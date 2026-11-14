# Просто переставить курсор (без клика) — чтобы проверить hover-состояние виджета.
param([Parameter(Mandatory = $true)][int]$X, [Parameter(Mandatory = $true)][int]$Y)

Add-Type -Namespace Mv -Name C -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
'@

[void][Mv.C]::SetCursorPos($X, $Y)
"cursor at $X,$Y"
