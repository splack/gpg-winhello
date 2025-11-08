#!/usr/bin/env pwsh
# Build script for GPG Windows Hello

$ErrorActionPreference = "Stop"

Write-Host "Building GPG Windows Hello..." -ForegroundColor Cyan

# Clean
if (Test-Path "bin") {
    Remove-Item -Recurse -Force bin
}
if (Test-Path "obj") {
    Remove-Item -Recurse -Force obj
}

# Build
Write-Host "`nBuilding gpg-winhello.exe..." -ForegroundColor Yellow
dotnet publish -c Release -o bin/release -p:AssemblyName=gpg-winhello

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nBuild complete!" -ForegroundColor Green
Write-Host "Executable: bin/release/gpg-winhello.exe" -ForegroundColor Green
Write-Host ""
Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  Setup:    gpg-winhello.exe setup" -ForegroundColor White
Write-Host "  Pinentry: gpg-winhello.exe (used by gpg-agent)" -ForegroundColor White
