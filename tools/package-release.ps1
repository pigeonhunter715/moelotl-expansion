[CmdletBinding()]
param([string]$ConfigPath = (Join-Path $PSScriptRoot '../build.local.json'))
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
& (Join-Path $repo 'build.ps1') -ConfigPath $ConfigPath -RunTests
& python (Join-Path $PSScriptRoot 'validate.py')
if ($LASTEXITCODE -ne 0) { throw 'Static validation failed.' }
& python (Join-Path $PSScriptRoot 'release_zip.py')
if ($LASTEXITCODE -ne 0) { throw 'Release packaging failed.' }
