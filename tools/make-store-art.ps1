# Draws the Microsoft Store logos into packaging\msix\listing:
#   store-boxart-2160.png     1:1 box art (2160 x 2160)
#   store-poster-1440x2160.png 9:16 poster art (1440 x 2160)
# Reuses the logo drawing code from make-icon.ps1.

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$iconScript = Get-Content (Join-Path $PSScriptRoot "make-icon.ps1") -Raw
Invoke-Expression ($iconScript.Substring(0, $iconScript.IndexOf('$sizes = ')))   # load the drawing functions only

$out = (Resolve-Path (Join-Path $PSScriptRoot "..\packaging\msix\listing")).Path

function New-Art([int]$W, [int]$H, [int]$logo, [int]$logoTop, [bool]$tagline, [string]$file) {
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = "AntiAlias"; $g.InterpolationMode = "HighQualityBicubic"; $g.TextRenderingHint = "AntiAliasGridFit"

    # Background: dark green-black gradient, a soft glow and a faint grid, as on the website
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 0, $H),
        ([System.Drawing.Color]::FromArgb(11, 15, 13)), ([System.Drawing.Color]::FromArgb(16, 26, 20))
    $g.FillRectangle($bg, 0, 0, $W, $H)
    $glow = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cx = $W / 2; $cy = $logoTop + $logo / 2
    $glow.AddEllipse([float]($cx - $logo), [float]($cy - $logo), [float]($logo * 2), [float]($logo * 2))
    $pg = New-Object System.Drawing.Drawing2D.PathGradientBrush $glow
    $pg.CenterColor = [System.Drawing.Color]::FromArgb(70, 34, 197, 94)
    $pg.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 34, 197, 94))
    $g.FillPath($pg, $glow)
    $step = [int]($W / 30)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(18, 255, 255, 255)), ([Math]::Max(1, $W / 1400))
    for ($x = 0; $x -le $W; $x += $step) { $g.DrawLine($pen, $x, 0, $x, $H) }
    for ($y = 0; $y -le $H; $y += $step) { $g.DrawLine($pen, 0, $y, $W, $y) }

    # Logo
    $icon = Render $logo
    $g.DrawImage($icon, [int](($W - $logo) / 2), $logoTop, $logo, $logo)
    $icon.Dispose()

    # Name (and tagline on the poster)
    $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = "Center"
    $nameFont = New-Object System.Drawing.Font "Segoe UI Semibold", ([float]($W * 0.105)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $nameTop = $logoTop + $logo + $W * 0.05
    $g.DrawString("SerialScope", $nameFont, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(231, 238, 233))),
        (New-Object System.Drawing.RectangleF 0, ([float]$nameTop), $W, ($W * 0.2)), $fmt)
    if ($tagline) {
        $tagFont = New-Object System.Drawing.Font "Segoe UI", ([float]($W * 0.045)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
        $g.DrawString("Serial monitor and live plotter`nfor ESP32, Arduino and more", $tagFont,
            (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(154, 168, 160))),
            (New-Object System.Drawing.RectangleF 0, ([float]($nameTop + $W * 0.16)), $W, ($W * 0.3)), $fmt)
    }
    $g.Dispose()
    $path = Join-Path $out $file
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Host "Wrote $path"
}

New-Art 2160 2160 1150 360 $false "store-boxart-2160.png"
New-Art 1440 2160 860 520 $true "store-poster-1440x2160.png"
