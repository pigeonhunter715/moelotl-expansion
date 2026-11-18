[CmdletBinding()]
param([string]$ConfigPath = (Join-Path $PSScriptRoot '../build.local.json'))
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$runtime = Join-Path $repo 'artifacts/runtime'
$game = Join-Path $runtime 'game'
$save = Join-Path $runtime 'save'
# This script uses a prepared, isolated game directory. Never use a player's game directory.
if (-not (Test-Path -LiteralPath (Join-Path $game 'RimWorldWin64.exe'))) { throw 'Prepare artifacts/runtime/game with the 1.6 executable, Data/Managed junctions, and only the required dependency Mods junctions first.' }
$running = Get-Process -Name RimWorldWin64 -ErrorAction SilentlyContinue | Where-Object Path -eq (Join-Path $game 'RimWorldWin64.exe')
if ($running) { throw 'Close the previous isolated test process before rebuilding.' }
$testMod = Join-Path $game 'Mods/MXRuntimeTests'
New-Item -ItemType Directory -Force -Path (Join-Path $testMod 'About'), (Join-Path $testMod 'Assemblies'), (Join-Path $save 'Config') | Out-Null
'<ModMetaData><name>MoeLotl Expansion Isolated Tests</name><author>local test harness</author><packageId>pigeonhunter715.moelotl.expansion.tests</packageId><supportedVersions><li>1.6</li></supportedVersions><loadAfter><li>pigeonhunter715.moelotl.expansion</li></loadAfter></ModMetaData>' | Set-Content -LiteralPath (Join-Path $testMod 'About/About.xml') -Encoding utf8
$references = @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { Join-Path $config.frameworkReferenceDir $_ }
$references += @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.ScreenCaptureModule.dll', 'netstandard.dll') | ForEach-Object { Join-Path $config.rimWorldManagedDir $_ }
$references += @($config.axolotlDll, $config.rigorMortisDll, $config.harmonyDll, (Join-Path $repo '1.6/Assemblies/MoeLotlExpansion.dll'))
$argsCompile = @('/noconfig', '/nologo', '/nostdlib+', '/target:library', '/langversion:latest', '/warnaserror+', ('/out:' + (Join-Path $testMod 'Assemblies/MXRuntimeTests.dll')))
$argsCompile += $references | ForEach-Object { '/reference:' + $_ }
$argsCompile += Join-Path $repo 'Tests/RuntimeSmoke.cs'
& $config.compilerPath @argsCompile
if ($LASTEXITCODE -ne 0) { throw 'Smoke harness compilation failed.' }
$modsPath = Join-Path $save 'Config/ModsConfig.xml'
$prefsPath = Join-Path $save 'Config/Prefs.xml'
if (Test-Path -LiteralPath $prefsPath) {
    [xml]$prefs = Get-Content -LiteralPath $prefsPath -Raw
    $prefs.PrefsData.runInBackground = 'True'; $prefs.PrefsData.volumeMaster = '0'; $prefs.PrefsData.uiScale = '1'; $prefs.Save($prefsPath)
}
[xml]$mods = Get-Content -LiteralPath $modsPath -Raw
if (-not ($mods.ModsConfigData.activeMods.li -contains 'pigeonhunter715.moelotl.expansion.tests')) {
    $entry = $mods.CreateElement('li'); $entry.InnerText = 'pigeonhunter715.moelotl.expansion.tests'; $mods.ModsConfigData.activeMods.AppendChild($entry) | Out-Null; $mods.Save($modsPath)
}
$result = Join-Path $save 'mx-smoke.txt'
if (Test-Path -LiteralPath $result) { Move-Item -LiteralPath $result -Destination (Join-Path $save ('mx-smoke-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.txt')) }
$launchArgs = @('-quicktest', '-mx-smoke', '-screen-width', '1280', '-screen-height', '900', '-screen-fullscreen', '0', ('-savedatafolder="' + $save + '"'), '-logFile', ('"' + (Join-Path $runtime 'smoke.log') + '"'))
$process = Start-Process -FilePath (Join-Path $game 'RimWorldWin64.exe') -ArgumentList $launchArgs -WorkingDirectory $game -WindowStyle Hidden -PassThru
$process.Id | Set-Content -LiteralPath (Join-Path $runtime 'process-id.txt')
Write-Output ('Started isolated smoke test PID ' + $process.Id + '; result: ' + $result)
