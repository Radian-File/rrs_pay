[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $false,
    [string]$OutputPath = "artifacts\portfolio-publish"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "rrs_pay\rrs_pay.csproj"
$resolvedOutput = Join-Path $repoRoot $OutputPath
$selfContainedArg = if ($SelfContained) { "true" } else { "false" }

Write-Host "Publishing RRS Pay portfolio build..." -ForegroundColor Cyan
Write-Host "Project: $projectPath"
Write-Host "Configuration: $Configuration"
Write-Host "Runtime: $Runtime"
Write-Host "Self-contained: $selfContainedArg"
Write-Host "Output: $resolvedOutput"

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

& dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }

& dotnet publish $projectPath -c $Configuration -r $Runtime --self-contained $selfContainedArg -o $resolvedOutput
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host "Portfolio publish completed: $resolvedOutput" -ForegroundColor Green
