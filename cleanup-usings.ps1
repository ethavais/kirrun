# Script to remove unused usings in the project
# This script uses 'dotnet format' which respects the rules in .editorconfig
$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$sln = Join-Path $repoRoot "KillRun.slnx"
if (-not (Test-Path -LiteralPath $sln)) {
    throw "Solution not found: KillRun.slnx"
}

Write-Host ">>> Scanning and removing unused usings (workspace: $sln)..." -ForegroundColor Cyan

dotnet format style $sln --severity warn

if ($LASTEXITCODE -eq 0) {
    Write-Host ">>> Completed! Unused usings have been removed." -ForegroundColor Green
} else {
    Write-Host ">>> An error occurred during formatting." -ForegroundColor Red
}