$ErrorActionPreference = 'Stop'
$root = Join-Path $env:RUNNER_TEMP 'TtroClient-Installer-QA'
$setup = Join-Path $PSScriptRoot '../dist/TtroClient-Setup.exe'
$p = Start-Process -FilePath $setup -ArgumentList @('/S', "/D=$root") -PassThru
if (!$p.WaitForExit(90000) -or $p.ExitCode -ne 0) { throw 'Installer did not complete successfully' }
$launcher = Join-Path $root 'TtroClient.exe'
if (!(Test-Path $launcher)) { throw 'Installed launcher missing' }
Write-Host 'PASS silent per-user installation produced native launcher'
& (Join-Path $PSScriptRoot 'windows_launcher_qa.ps1') -LauncherPath $launcher
$profileStore = Join-Path $env:LOCALAPPDATA 'TtroClient189/native/launcher.json'
$hash = (Get-FileHash $profileStore).Hash
$p = Start-Process -FilePath (Join-Path $root 'Uninstall.exe') -ArgumentList '/S' -PassThru
if (!$p.WaitForExit(30000) -or $p.ExitCode -ne 0) { throw 'Uninstaller failed' }
if (Test-Path $launcher) { throw 'Uninstaller retained installed launcher' }
if (!(Test-Path $profileStore) -or (Get-FileHash $profileStore).Hash -ne $hash) { throw 'Uninstaller changed profile store' }
Write-Host 'PASS uninstall preserves profiles and removes program files'
@{ status='PASS'; environment='Windows GitHub Actions runner'; checks=@('Silent installation', 'Installed native UI automation', 'Uninstall removes application', 'Uninstall preserves profile store'); limits=@('No Microsoft account', 'No SmartScreen/standard-user certification', 'No physical input or hardware performance') } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $PSScriptRoot '../research/windows-installer-smoke.json')
