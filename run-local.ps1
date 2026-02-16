#!/usr/bin/env pwsh
# Start the MicroHack environment using .NET Aspire
# Usage: ./run-local.ps1
#
# This launches the Aspire AppHost which orchestrates:
#   - API (C# backend)
#   - Web (Next.js frontend)
#   - PostgreSQL database
#
# The Aspire dashboard opens automatically in your browser
# with logs, traces, and endpoints for all services.
param(
    [switch]$CleanBuild,
    [switch]$CleanOnly
)

Set-StrictMode -Version Latest
$webDir = Join-Path $PSScriptRoot 'src' 'web'
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    if($CleanBuild -or $CleanOnly) {
        Write-Host "Cleaning Aspire environment..." -ForegroundColor Yellow
        dotnet aspire clean
    
        # Clean Next.js cache and reinstall dependencies
        Write-Host "Cleaning web build cache..." -ForegroundColor Cyan
        if (Test-Path (Join-Path $webDir '.next'))   { Remove-Item (Join-Path $webDir '.next') -Recurse -Force }
        if (Test-Path (Join-Path $webDir 'node_modules')) { Remove-Item (Join-Path $webDir 'node_modules') -Recurse -Force }
    }
    if($CleanOnly) {
        Write-Host "Clean complete. Exiting." -ForegroundColor Green
        return
    }

    Write-Host "Installing web dependencies..." -ForegroundColor Cyan
    Push-Location $webDir
    npm install
    Pop-Location
    
    Write-Host "Starting Aspire AppHost..." -ForegroundColor Cyan
    dotnet run --project src/AppHost
}
finally {
    Pop-Location
}
