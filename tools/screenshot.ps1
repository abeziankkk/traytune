# Снимок экрана + кроп правого нижнего угла (где живёт виджет).
# Запускать через powershell.exe (5.1): Add-Type System.Drawing там доступен из коробки.
param(
    [string]$Out = "$PSScriptRoot",
    [int]$CropWidth = 560,
    [int]$CropHeight = 320,
    [switch]$FromLeft
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $bmp.Size)
$g.Dispose()

$full = Join-Path $Out "full.png"
$bmp.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)

$w = [Math]::Min($CropWidth, $bmp.Width)
$h = [Math]::Min($CropHeight, $bmp.Height)
$x = if ($FromLeft) { 0 } else { $bmp.Width - $w }
$rect = New-Object System.Drawing.Rectangle $x, ($bmp.Height - $h), $w, $h
$crop = $bmp.Clone($rect, $bmp.PixelFormat)
$corner = Join-Path $Out "corner.png"
$crop.Save($corner, [System.Drawing.Imaging.ImageFormat]::Png)

$crop.Dispose()
$bmp.Dispose()

"screen: $($vs.Width)x$($vs.Height) -> $full"
"crop:   $w x $h -> $corner"
