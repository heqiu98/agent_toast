# Build agent_toast.exe from src/*.cs with csc.exe, embedding assets/app.ico as the exe icon.
$ErrorActionPreference = "Stop"
$srcDir = Join-Path $PSScriptRoot "src"
$outDir = Join-Path $PSScriptRoot "release"
$out = Join-Path $outDir "agent_toast.exe"
$icon = Join-Path $PSScriptRoot "assets\app.ico"
New-Item -ItemType Directory -Force $outDir | Out-Null

# regenerate the icon from assets/make-icon.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "assets\make-icon.ps1") | Out-Null
if (-not (Test-Path $icon)) { throw "icon not found: $icon" }

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }

$args = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/out:$out",
    "/win32icon:$icon",
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Web.Extensions.dll",
    (Join-Path $srcDir "core.cs"),
    (Join-Path $srcDir "app.cs"),
    (Join-Path $srcDir "gui.cs"),
    (Join-Path $srcDir "tabs.cs")
)
& $csc @args
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
Write-Host "Built: $out"
