# Generates docs\banner.png (1280x640): README header and GitHub social preview image.
# Uses docs\icon.png and docs\screenshot-plotter.png, so run make-icon.ps1 first.
# Usage:  powershell -ExecutionPolicy Bypass -File tools\make-banner.ps1

Add-Type -AssemblyName System.Drawing

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$W = 1280; $H = 640

function C([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.InterpolationMode = 'HighQualityBicubic'
$g.TextRenderingHint = 'AntiAliasGridFit'

# Background: near-black with a soft green glow on the left and a fine graticule
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point $W, $H), (C 255 20 20 23), (C 255 9 9 11)
$g.FillRectangle($bg, 0, 0, $W, $H)

$glow = New-Object System.Drawing.Drawing2D.GraphicsPath
$glow.AddEllipse(-300, -200, 1100, 1000)
$glowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush $glow
$glowBrush.CenterColor = C 40 34 197 94
$glowBrush.SurroundColors = [System.Drawing.Color[]]@((C 0 34 197 94))
$g.FillPath($glowBrush, $glow)

$grid = New-Object System.Drawing.Pen (C 12 255 255 255), 1
for ($x = 0; $x -le $W; $x += 40) { $g.DrawLine($grid, $x, 0, $x, $H) }
for ($y = 0; $y -le $H; $y += 40) { $g.DrawLine($grid, 0, $y, $W, $y) }

# Screenshot on the right, running off the edge, with a shadow and rounded corners
$shot = [System.Drawing.Image]::FromFile("$root\docs\screenshot-plotter.png")
$sw = 760; $sh = [int]($shot.Height * $sw / $shot.Width)
$sx = 610; $sy = [int](($H - $sh) / 2) + 10
for ($k = 18; $k -ge 1; $k--) {
    $shadow = New-RoundedRect ($sx - $k) ($sy - $k + 12) ($sw + 2 * $k) ($sh + 2 * $k) (14 + $k)
    $g.FillPath((New-Object System.Drawing.SolidBrush (C 9 0 0 0)), $shadow)
}
$frame = New-RoundedRect $sx $sy $sw $sh 14
$g.SetClip($frame)
$g.DrawImage($shot, $sx, $sy, $sw, $sh)
$g.ResetClip()
$g.DrawPath((New-Object System.Drawing.Pen (C 60 255 255 255), 1), $frame)

# Fade the screenshot into the right edge
$fade = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point ($W - 140), 0), (New-Object System.Drawing.Point $W, 0), (C 0 9 9 11), (C 235 9 9 11)
$g.FillRectangle($fade, $W - 140, 0, 140, $H)

# Left column: logo, name, tagline, highlights
$icon = [System.Drawing.Image]::FromFile("$root\docs\icon.png")
$g.DrawImage($icon, 72, 150, 112, 112)

$titleFont = New-Object System.Drawing.Font "Segoe UI Semibold", 54, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$g.DrawString("SerialScope", $titleFont, (New-Object System.Drawing.SolidBrush (C 255 250 250 250)), 64, 282)

$tagFont = New-Object System.Drawing.Font "Segoe UI", 22, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$muted = New-Object System.Drawing.SolidBrush (C 255 161 161 170)
$g.DrawString("Serial monitor and live plotter`nfor ESP32, Arduino and more.", $tagFont, $muted, 70, 356)

# Highlight chips
$chipFont = New-Object System.Drawing.Font "Segoe UI Semibold", 15, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$x = 72
foreach ($label in "Free & open source", "No install", "Windows 10 / 11") {
    $size = $g.MeasureString($label, $chipFont)
    $cw = [int]$size.Width + 26
    $chip = New-RoundedRect $x 448 $cw 34 17
    $g.FillPath((New-Object System.Drawing.SolidBrush (C 26 34 197 94)), $chip)
    $g.DrawPath((New-Object System.Drawing.Pen (C 110 34 197 94), 1), $chip)
    $g.DrawString($label, $chipFont, (New-Object System.Drawing.SolidBrush (C 255 134 239 172)), $x + 13, 455)
    $x += $cw + 10
}

$g.Dispose()
$out = "$root\docs\banner.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$shot.Dispose(); $icon.Dispose(); $bmp.Dispose()
Write-Host "Wrote $out"
