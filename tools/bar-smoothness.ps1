# Измеряет длину заливки полосы прогресса по кадрам (полоса = нижняя строка виджета).
param(
    [int]$Frames = 6,
    [int]$DelayMs = 400,
    [int]$Left = 8,
    [int]$Top = 1035,
    [int]$Width = 340,
    [int]$Height = 42
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
$widths = @()

for ($i = 0; $i -lt $Frames; $i++) {
    $bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $bmp.Size)
    $g.Dispose()

    $best = 0
    # нижние 10 строк окна — там полоса
    for ($y = $Top + $Height - 10; $y -lt ($Top + $Height); $y++) {
        $run = 0
        for ($x = $Left; $x -lt ($Left + $Width); $x++) {
            if ($x -ge $bmp.Width -or $y -ge $bmp.Height) { break }
            $c = $bmp.GetPixel($x, $y)
            $isAccent = ($c.G -gt 110 -and $c.G -gt ($c.R + 25) -and $c.G -gt ($c.B + 25)) -or
                        ($c.R -gt 190 -and $c.G -gt 90 -and $c.B -lt 130)
            if ($isAccent) { $run++ } else { if ($run -gt $best) { $best = $run }; $run = 0 }
        }
        if ($run -gt $best) { $best = $run }
    }

    $widths += $best
    $bmp.Dispose()
    Start-Sleep -Milliseconds $DelayMs
}

"заливка по кадрам: $($widths -join ', ')"
$deltas = @()
for ($i = 1; $i -lt $widths.Count; $i++) { $deltas += ($widths[$i] - $widths[$i-1]) }
"приросты: $($deltas -join ', ')"
"назад: $(($deltas | Where-Object { $_ -lt 0 }).Count)"
