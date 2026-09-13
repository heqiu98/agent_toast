# Build codex-toast.exe from src/*.cs. Requires Windows PowerShell (Add-Type / CodeDOM).
$ErrorActionPreference = "Stop"
$srcDir = Join-Path $PSScriptRoot "src"
$outDir = Join-Path $PSScriptRoot "release"
$out = Join-Path $outDir "codex-toast.exe"
New-Item -ItemType Directory -Force $outDir | Out-Null

$usings = @"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
"@

$code = $usings + "`n"
foreach ($f in @("core.cs", "app.cs", "gui.cs")) {
    $text = [System.IO.File]::ReadAllText((Join-Path $srcDir $f))
    # strip per-file using directives; common set is prepended above
    $text = [regex]::Replace($text, '(?m)^\s*using\s+[\w\.]+;\s*\r?\n', '')
    $code += $text + "`n"
}

Add-Type -TypeDefinition $code `
    -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.Drawing.Primitives, System.Web.Extensions `
    -OutputAssembly $out -OutputType WindowsApplication
Write-Host "Built: $out"