# Р“РµРЅРµСЂРёСЂСѓРµС‚ app.ico: Р·РµР»С‘РЅС‹Р№ РєСЂСѓРі СЃ С‚С‘РјРЅС‹Рј С‚СЂРµСѓРіРѕР»СЊРЅРёРєРѕРј В«playВ».
# ICO СЃРѕР±РёСЂР°РµС‚СЃСЏ РІСЂСѓС‡РЅСѓСЋ: Р·Р°РіРѕР»РѕРІРѕРє + Р·Р°РїРёСЃРё + PNG-РґР°РЅРЅС‹Рµ (Vista+ СЌС‚Рѕ РїРѕРЅРёРјР°РµС‚).
param(
    [string]$Out = (Join-Path $PSScriptRoot "..\player\app.ico")
)

Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = @()

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $inset = [Math]::Max(0.5, $size * 0.03)
    $d = $size - 2 * $inset
    $green = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 74, 222, 128))
    $g.FillEllipse($green, [float]$inset, [float]$inset, [float]$d, [float]$d)
    $green.Dispose()

    # РўСЂРµСѓРіРѕР»СЊРЅРёРє В«playВ» СЃРѕ СЃРєСЂСѓРіР»РµРЅРёРµРј С‡РµСЂРµР· polygon.
    $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 16, 16, 20))
    $cx = $size * 0.54
    $cy = $size * 0.5
    $half = $size * 0.20
    $left = $cx - $size * 0.11
    $pts = @(
        (New-Object System.Drawing.PointF ([float]$left, [float]($cy - $half))),
        (New-Object System.Drawing.PointF ([float]$left, [float]($cy + $half))),
        (New-Object System.Drawing.PointF ([float]($cx + $size * 0.17), [float]$cy))
    )
    $g.FillPolygon($dark, $pts)
    $dark.Dispose()
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += , $ms.ToArray()
    $ms.Dispose()
}

$headerSize = 6 + 16 * $sizes.Count
$offset = $headerSize
$buffer = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter -ArgumentList $buffer

$bw.Write([UInt16]0)               # reserved
$bw.Write([UInt16]1)               # type = icon
$bw.Write([UInt16]$sizes.Count)

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $data = $pngs[$i]
    $dim = if ($size -eq 256) { 0 } else { $size }
    $bw.Write([Byte]$dim)                          # width
    $bw.Write([Byte]$dim)                          # height
    $bw.Write([Byte]0)                             # palette
    $bw.Write([Byte]0)                             # reserved
    $bw.Write([UInt16]1)                           # planes
    $bw.Write([UInt16]32)                          # bpp
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}

foreach ($data in $pngs) { $bw.Write($data) }
$bw.Flush()

$full = [System.IO.Path]::GetFullPath($Out)
[System.IO.File]::WriteAllBytes($full, $buffer.ToArray())
$bw.Dispose()
$buffer.Dispose()

"icon -> $full ($((Get-Item $full).Length) Р±Р°Р№С‚, $($sizes.Count) СЂР°Р·РјРµСЂРѕРІ)"

