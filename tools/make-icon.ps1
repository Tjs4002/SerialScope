# Generates src\app.ico (16-256 px) and docs\icon.png for SerialScope.
# The logo: "SC" drawn as a square-wave trace on an oscilloscope screen.
# Usage:  powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$green = [System.Drawing.Color]::FromArgb(34, 197, 94)
$greenTop = [System.Drawing.Color]::FromArgb(74, 222, 128)
$greenBottom = [System.Drawing.Color]::FromArgb(22, 170, 78)
$greenCore = [System.Drawing.Color]::FromArgb(220, 252, 231)

function C([int]$a, $c) { [System.Drawing.Color]::FromArgb($a, $c) }
function Pt([float]$s, [float]$x, [float]$y) { New-Object System.Drawing.PointF ($s * $x), ($s * $y) }

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

# Polyline with softly rounded corners (each corner replaced by a short curve)
function Add-RoundedPolyline($path, [System.Drawing.PointF[]]$pts, [float]$r) {
    $path.StartFigure()
    $prev = $pts[0]
    for ($i = 1; $i -lt $pts.Length - 1; $i++) {
        $c = $pts[$i]; $n = $pts[$i + 1]
        $d1 = [Math]::Sqrt(($c.X - $prev.X) * ($c.X - $prev.X) + ($c.Y - $prev.Y) * ($c.Y - $prev.Y))
        $d2 = [Math]::Sqrt(($n.X - $c.X) * ($n.X - $c.X) + ($n.Y - $c.Y) * ($n.Y - $c.Y))
        $rr = [Math]::Min($r, [Math]::Min($d1, $d2) / 2)
        $a = New-Object System.Drawing.PointF ($c.X - ($c.X - $prev.X) / $d1 * $rr), ($c.Y - ($c.Y - $prev.Y) / $d1 * $rr)
        $b = New-Object System.Drawing.PointF ($c.X + ($n.X - $c.X) / $d2 * $rr), ($c.Y + ($n.Y - $c.Y) / $d2 * $rr)
        $path.AddLine($prev, $a)
        $k = 0.55   # control-point factor for a near-circular quarter turn
        $c1 = New-Object System.Drawing.PointF ($a.X + ($c.X - $a.X) * $k * 1.8), ($a.Y + ($c.Y - $a.Y) * $k * 1.8)
        $c2 = New-Object System.Drawing.PointF ($b.X + ($c.X - $b.X) * $k * 1.8), ($b.Y + ($c.Y - $b.Y) * $k * 1.8)
        $path.AddBezier($a, $c1, $c2, $b)
        $prev = $b
    }
    $path.AddLine($prev, $pts[$pts.Length - 1])
}

# Letter geometry (fractions of the icon size)
$xLeft = 0.215; $xSRight = 0.445; $xCLeft = 0.555; $xRight = 0.785
$yTop = 0.25; $yMid = 0.50; $yBottom = 0.75; $yCTop = 0.355
$script:BeamEnd = @($xRight, $yBottom)

function New-Trace([float]$s) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = $s * 0.04
    # S: top line runs the full width, over the C
    Add-RoundedPolyline $path ([System.Drawing.PointF[]]@((Pt $s $xRight $yTop), (Pt $s $xLeft $yTop), (Pt $s $xLeft $yMid), (Pt $s $xSRight $yMid), (Pt $s $xSRight $yBottom), (Pt $s $xLeft $yBottom))) $r
    # C: one step lower, like the same wave shifted
    Add-RoundedPolyline $path ([System.Drawing.PointF[]]@((Pt $s $xRight $yCTop), (Pt $s $xCLeft $yCTop), (Pt $s $xCLeft $yBottom), (Pt $s $xRight $yBottom))) $r
    return $path
}

function New-Pen($brushOrColor, [float]$w) {
    $pen = New-Object System.Drawing.Pen $brushOrColor, $w
    $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    return $pen
}

function Draw-Logo($g, [float]$s) {
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $tile = New-RoundedRect 0.5 0.5 ($s - 1) ($s - 1) ($s * 0.225)

    # Screen: dark gradient
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, $s),
        ([System.Drawing.Color]::FromArgb(40, 40, 46)), ([System.Drawing.Color]::FromArgb(14, 14, 17))
    $g.FillPath($bg, $tile)
    $g.SetClip($tile)

    # Soft green light behind the trace
    if ($s -ge 32) {
        $glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $glowPath.AddEllipse($s * 0.05, $s * 0.08, $s * 0.9, $s * 0.84)
        $radial = New-Object System.Drawing.Drawing2D.PathGradientBrush $glowPath
        $radial.CenterColor = C 34 $green
        $radial.SurroundColors = [System.Drawing.Color[]]@((C 0 $green))
        $g.FillPath($radial, $glowPath)
    }

    # Graticule: 8x8 grid, centre axes with small ticks
    if ($s -ge 64) {
        $lw = [Math]::Max(1, $s / 256)
        $minor = New-Object System.Drawing.Pen (C 13 ([System.Drawing.Color]::White)), $lw
        $axis = New-Object System.Drawing.Pen (C 26 ([System.Drawing.Color]::White)), $lw
        for ($i = 1; $i -lt 8; $i++) {
            $p = $s * $i / 8
            $pen = if ($i -eq 4) { $axis } else { $minor }
            $g.DrawLine($pen, $p, 0, $p, $s); $g.DrawLine($pen, 0, $p, $s, $p)
        }
        $tick = $s * 0.012
        for ($i = 1; $i -lt 40; $i++) {
            $p = $s * $i / 40
            $g.DrawLine($axis, $p, $s / 2 - $tick, $p, $s / 2 + $tick)
            $g.DrawLine($axis, $s / 2 - $tick, $p, $s / 2 + $tick, $p)
        }
    }

    $w = [Math]::Max(1.6, $s * 0.056)
    $trace = New-Trace $s

    # Phosphor glow: many faint layers fading smoothly
    if ($s -ge 48) {
        for ($k = 12; $k -ge 1; $k--) {
            $g.DrawPath((New-Pen (C 7 $green) ($w * (1 + $k * 0.26))), $trace)
        }
    }

    # Trace with a top-to-bottom shade, then a bright core
    $shade = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, ($s * $yTop)), (New-Object System.Drawing.PointF 0, ($s * $yBottom + 1)), $greenTop, $greenBottom
    $g.DrawPath((New-Pen $shade $w), $trace)
    if ($s -ge 48) { $g.DrawPath((New-Pen (C 140 $greenCore) ($w * 0.28)), $trace) }

    # Beam spot where the trace ends
    if ($s -ge 32) {
        $cx = $s * $script:BeamEnd[0]; $cy = $s * $script:BeamEnd[1]; $r = $w * 0.92
        if ($s -ge 48) {
            for ($k = 9; $k -ge 1; $k--) {
                $rr = $r * (1 + $k * 0.24)
                $g.FillEllipse((New-Object System.Drawing.SolidBrush (C 11 $green)), $cx - $rr, $cy - $rr, $rr * 2, $rr * 2)
            }
        }
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $greenTop), $cx - $r, $cy - $r, $r * 2, $r * 2)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $greenCore), $cx - $r * 0.45, $cy - $r * 0.45, $r * 0.9, $r * 0.9)
    }

    # Vignette: darker towards the edges, like a CRT
    if ($s -ge 48) {
        $vig = New-Object System.Drawing.Drawing2D.GraphicsPath
        $vig.AddEllipse(-$s * 0.25, -$s * 0.25, $s * 1.5, $s * 1.5)
        $vb = New-Object System.Drawing.Drawing2D.PathGradientBrush $vig
        $vb.CenterColor = C 0 ([System.Drawing.Color]::Black)
        $vb.SurroundColors = [System.Drawing.Color[]]@((C 120 ([System.Drawing.Color]::Black)))
        $vb.FocusScales = New-Object System.Drawing.PointF 0.55, 0.55
        $g.FillPath($vb, $vig)
    }
    $g.ResetClip()

    # Edge: faint light rim, brighter along the top
    $rim = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 0, $s),
        (C 46 ([System.Drawing.Color]::White)), (C 8 ([System.Drawing.Color]::White))
    $g.DrawPath((New-Object System.Drawing.Pen $rim, ([Math]::Max(1, $s / 180))), $tile)
}

function Render([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    Draw-Logo $g $s
    $g.Dispose(); return $bmp
}

function Get-Png([int]$s) {
    $bmp = Render $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($s in $sizes) { , (Get-Png $s) }

# ICO container with PNG-compressed entries (supported since Windows Vista)
$icoPath = (Resolve-Path (Join-Path $PSScriptRoot "..\src")).Path + "\app.ico"
$fs = [System.IO.File]::Create($icoPath)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$len); $w.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $w.Write($img) }
$w.Close()
Write-Host "Wrote $icoPath"

# Large PNG for the README
$pngPath = (Resolve-Path (Join-Path $PSScriptRoot "..\docs")).Path + "\icon.png"
[System.IO.File]::WriteAllBytes($pngPath, $images[-1])
Write-Host "Wrote $pngPath"
