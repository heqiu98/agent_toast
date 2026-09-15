# Generates assets/app.ico: dark rounded square + white bell + blue clapper (PNG-in-ICO).
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"
$assets = $PSScriptRoot
New-Item -ItemType Directory -Force $assets | Out-Null
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
$bg = New-Object System.Drawing.Drawing2D.GraphicsPath
Add-RoundedRect $bg 8 8 240 240 52
$bgRect = New-Object System.Drawing.Rectangle 8, 8, 240, 240
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($bgRect, [System.Drawing.Color]::FromArgb(255, 44, 54, 72), [System.Drawing.Color]::FromArgb(255, 17, 22, 33), [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
$g.FillPath($bgBrush, $bg)

$bell = New-Object System.Drawing.Drawing2D.GraphicsPath
$bell.AddArc(74, 116, 108, 104, 180, 180)
$bell.AddArc(56, 168, 144, 36, 0, 180)
$bell.AddLine(56, 186, 74, 168)
$bell.CloseFigure()
$white = [System.Drawing.Color]::FromArgb(255, 242, 246, 252)
$whiteBrush = New-Object System.Drawing.SolidBrush $white
$g.FillPath($whiteBrush, $bell)

$g.FillEllipse($whiteBrush, (New-Object System.Drawing.Rectangle 115, 94, 26, 26))

$accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 102, 192, 244))
$g.FillEllipse($accent, (New-Object System.Drawing.Rectangle 113, 193, 30, 30))

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
