# Вырезать область из PNG: crop.ps1 -In full.png -X 740 -Y 340 -W 440 -H 350 -Out settings.png
param(
    [Parameter(Mandatory = $true)][string]$In,
    [Parameter(Mandatory = $true)][int]$X,
    [Parameter(Mandatory = $true)][int]$Y,
    [Parameter(Mandatory = $true)][int]$W,
    [Parameter(Mandatory = $true)][int]$H,
    [Parameter(Mandatory = $true)][string]$Out
)

Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Image]::FromFile((Resolve-Path $In))
try {
    $rect = New-Object System.Drawing.Rectangle $X, $Y, $W, $H
    $crop = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $W, $H), $rect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $crop.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $crop.Dispose()
}
finally {
    $src.Dispose()
}

"cropped -> $Out"
