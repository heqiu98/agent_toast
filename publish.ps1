# Build agent_toast.exe and package a release zip for GitHub Releases.
# Usage: ./publish.ps1 1.0.0
# NOTE: personal/custom sounds in release/sounds/ are NOT packaged
#       (game voice lines are copyrighted; users import their own).
param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference = "Stop"

powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build.ps1")

$zip = Join-Path $PSScriptRoot "agent_toast-v$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $PSScriptRoot "release\agent_toast.exe") -DestinationPath $zip
Write-Host "Release asset: $zip"