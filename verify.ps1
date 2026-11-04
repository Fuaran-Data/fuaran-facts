#Requires -Version 7.0
# The gate: a pass-through to run.ps1 with every stage on.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$global:LASTEXITCODE = 0
& (Join-Path $PSScriptRoot 'run.ps1')
exit $global:LASTEXITCODE
