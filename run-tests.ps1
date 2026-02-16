#!/usr/bin/env pwsh
# Run all tests from the repo root
# Usage: ./run-tests.ps1 [unit|e2e|cucumber|all]

param(
    [ValidateSet("unit", "e2e", "cucumber", "all")]
    [string]$Suite = "all"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    switch ($Suite) {
        "unit" {
            Write-Host "`n=== .NET Unit & Integration Tests ===" -ForegroundColor Cyan
            dotnet test src/api --verbosity minimal
        }
        "e2e" {
            Write-Host "`n=== Playwright E2E Tests ===" -ForegroundColor Cyan
            npx playwright test --config=tests/e2e/playwright.config.ts
        }
        "cucumber" {
            Write-Host "`n=== Cucumber/Gherkin Tests ===" -ForegroundColor Cyan
            Push-Location tests
            npx cucumber-js --config cucumber.js
            Pop-Location
        }
        "all" {
            Write-Host "`n=== .NET Unit & Integration Tests ===" -ForegroundColor Cyan
            dotnet test src/api --verbosity minimal

            Write-Host "`n=== Cucumber/Gherkin Tests ===" -ForegroundColor Cyan
            Push-Location tests
            npx cucumber-js --config cucumber.js
            Pop-Location

            Write-Host "`n=== Playwright E2E Tests ===" -ForegroundColor Cyan
            npx playwright test --config=tests/e2e/playwright.config.ts
        }
    }
}
finally {
    Pop-Location
}
