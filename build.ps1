# Build codex-toast.exe from source. Requires Windows PowerShell (Add-Type / CodeDOM).
$ErrorActionPreference = "Stop"
$src = Join-Path $PSScriptRoot "src\codex-toast.cs"
$outDir = Join-Path $PSScriptRoot "release"
$out = Join-Path $outDir "codex-toast.exe"
New-Item -ItemType Directory -Force $outDir | Out-Null
Add-Type -TypeDefinition ([System.IO.File]::ReadAllText($src)) `
    -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Drawing.Primitives `
    -OutputAssembly $out -OutputType WindowsApplication
Write-Host "Built: $out"