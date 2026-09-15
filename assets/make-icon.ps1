# Generates assets/app.ico: dark rounded square + toast slice (butter pat + steam). PNG-in-ICO.
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
$assets = $PSScriptRoot
$icoPath = Join-Path $assets "app.ico"

$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

function Add-RoundedRect($path, [int]$x, [int]$y, [int]$w, [int]$h, [int]$r) {
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
}

# --- background: dark rounded square, vertical gradient ---
$bg = New-Object System.Drawing.Drawing2D.GraphicsPath
Add-RoundedRect $bg 8 8 240 240 52
$bgRect = New-Object System.Drawing.Rectangle 8, 8, 240, 240
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($bgRect, [System.Drawing.Color]::FromArgb(255, 44, 54, 72), [System.Drawing.Color]::FromArgb(255, 17, 22, 33), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$g.FillPath($bgBrush, $bg)

# --- steam (two wavy strokes above the toast) ---
$steamPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(210, 216, 224, 238)), 8
$steamPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$steamPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawBezier($steamPen, 104, 52, 93, 38, 115, 28, 104, 12)
$g.DrawBezier($steamPen, 150, 52, 139, 38, 161, 28, 150, 12)

# --- crust ---
$crust = New-Object System.Drawing.Drawing2D.GraphicsPath
$crust.AddArc(66, 62, 124, 124, 180, 180)
$crust.AddLine(190, 124, 190, 182)
$crust.AddArc(158, 166, 32, 32, 0, 90)
$crust.AddLine(174, 198, 82, 198)
$crust.AddArc(66, 166, 32, 32, 90, 90)
$crust.AddLine(66, 182, 66, 124)
$crust.CloseFigure()
$crustBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 216, 149, 90))
$g.FillPath($crustBrush, $crust)

# --- bread interior (gradient) ---
$bread = New-Object System.Drawing.Drawing2D.GraphicsPath
$bread.AddArc(80, 76, 96, 96, 180, 180)
$bread.AddLine(176, 124, 176, 174)
$bread.AddArc(156, 164, 20, 20, 0, 90)
$bread.AddLine(166, 184, 90, 184)
$bread.AddArc(80, 164, 20, 20, 90, 90)
$bread.AddLine(80, 174, 80, 124)
$bread.CloseFigure()
$breadRect = New-Object System.Drawing.Rectangle 80, 76, 96, 108
$breadBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($breadRect, [System.Drawing.Color]::FromArgb(255, 248, 222, 170), [System.Drawing.Color]::FromArgb(255, 237, 194, 126), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$g.FillPath($breadBrush, $bread)

# --- butter pat (slightly rotated) ---
$g.TranslateTransform(128, 146)
$g.RotateTransform(-8)
$butter = New-Object System.Drawing.Drawing2D.GraphicsPath
Add-RoundedRect $butter -19 -14 38 28 6
$butterBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 253, 233, 168))
$g.FillPath($butterBrush, $butter)
$g.ResetTransform()

# save PNG then wrap into ICO
$pngPath = Join-Path $env:TEMP "agent_toast_icon.png"
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

$pngBytes = [System.IO.File]::ReadAllBytes($pngPath)
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)
$bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)
$bw.Write([uint16]1); $bw.Write([uint16]32)
$bw.Write([uint32]$pngBytes.Length); $bw.Write([uint32]22)
$bw.Write($pngBytes)
$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$bw.Dispose(); $ms.Dispose()
Copy-Item $pngPath (Join-Path $assets "app.png") -Force
Write-Host "icon written: $icoPath"
