[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'build.local.json'),
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw 'Copy build.example.json to build.local.json and configure your own dependency paths.'
}
$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
foreach ($name in @('compilerPath', 'frameworkReferenceDir', 'rimWorldManagedDir', 'axolotlDll', 'rigorMortisDll', 'harmonyDll')) {
    if (-not $config.$name -or -not (Test-Path -LiteralPath $config.$name)) { throw "Missing dependency path: $name" }
}
if (-not (Test-Path -LiteralPath (Join-Path $config.rimWorldManagedDir 'UnityEngine.CoreModule.dll'))) {
    throw 'Expected the RimWorld 1.6 Unity Managed directory, not the 1.7 game root.'
}
$frameworkNames = @('mscorlib.dll', 'System.dll', 'System.Core.dll')
$references = @($frameworkNames | ForEach-Object { Join-Path $config.frameworkReferenceDir $_ })
$references += @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.IMGUIModule.dll', 'UnityEngine.TextRenderingModule.dll', 'netstandard.dll') | ForEach-Object { Join-Path $config.rimWorldManagedDir $_ }
$references += @($config.axolotlDll, $config.rigorMortisDll, $config.harmonyDll)
foreach ($path in $references) { if (-not (Test-Path -LiteralPath $path)) { throw "Missing reference: $path" } }
$outputDir = Join-Path $PSScriptRoot '1.6/Assemblies'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$assemblyPath = Join-Path $outputDir 'MoeLotlExpansion.dll'
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Source') -Recurse -Filter '*.cs' -File | Where-Object { $_.FullName.Replace([char]92, [char]47) -notmatch '/(obj|bin)/' } | Sort-Object FullName | Select-Object -ExpandProperty FullName)
$compilerArgs = @('/noconfig', '/nologo', '/nostdlib+', '/target:library', '/langversion:latest', '/optimize+', '/deterministic+', '/warn:4', '/warnaserror+', "/out:$assemblyPath")
$compilerArgs += $references | ForEach-Object { "/reference:$_" }
$compilerArgs += $sourceFiles
& $config.compilerPath @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Mod compilation failed.' }
Write-Output "Built $assemblyPath"

if ($RunTests) {
    $testDir = Join-Path $PSScriptRoot 'artifacts/tests'
    New-Item -ItemType Directory -Force -Path $testDir | Out-Null
    $testExe = Join-Path $testDir 'MedicineRulesTests.exe'
    $testArgs = @('/noconfig', '/nologo', '/nostdlib+', '/target:exe', '/langversion:latest', '/warnaserror+', "/out:$testExe")
    $testArgs += $frameworkNames | ForEach-Object { "/reference:$(Join-Path $config.frameworkReferenceDir $_)" }
    $testArgs += @( (Join-Path $PSScriptRoot 'Source/Core/MedicineRules.cs'), (Join-Path $PSScriptRoot 'Tests/MedicineRulesTests.cs') )
    & $config.compilerPath @testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Medicine rule tests failed.' }
}
