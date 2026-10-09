param([Parameter(Mandatory=$true)][string]$Assets, [string]$Output='owner-e2e.json')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$assetsPath=(Resolve-Path $Assets).Path
$zip=[System.IO.Compression.ZipFile]::OpenRead((Join-Path $assetsPath 'TtroClient-Portable.zip'))
function ReadZipJson([string]$name) { $reader=[System.IO.StreamReader]::new($zip.GetEntry($name).Open()); try { $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() } }
try { $build=ReadZipJson 'build-info.json'; $settings=ReadZipJson 'launcher-settings.json' } finally { $zip.Dispose() }
if ([guid]$settings.microsoftClientId -eq [guid]::Empty) { throw 'Register the Ttro public client and create a new CI distribution first.' }
$steps=[ordered]@{
 freshSetup='このSetup.exeをクリーンなWindowsユーザーへ実際にインストールした'
 microsoftLogin='Ttro専用IDのMicrosoft Loginを実所有アカウントで完了した'
 minecraftOwnership='所有確認が成功し、正規Minecraft profileが取得された'
 play='LauncherのPLAYで初回のJava/Forge/ゲーム取得と起動を完了した'
 minecraft189='起動したMinecraftがJava Edition 1.8.9である'
 ttroLoaded='ゲーム内Ttro設定（Right Ctrl）が開きClientがロードされた'
 localWorld='Local Worldを実際に開き操作できた'
 restartReplay='ゲームとLauncherを終了して再起動し、再PLAYを完了した'
 repair='Prepare / Repairを実行して再PLAYを完了した'
 resourcePackCardInstall='Libraryで実providerのPack画像を見てカードINSTALL一回で導入した'
 resourcePackInGame='そのPackが実MinecraftのResource PacksとLocal Worldへ反映された'
 modInstall='Forge 1.8.9対応Modをカードから導入し、実ゲームでロードを確認した'
 uninstallKeepsData='Uninstall後もProfile/Worldが保持されていることを確認した'
}
Write-Host '実行済みの操作だけを記録します。Token、ユーザー名、パスワードは入力しないでください。'
$checks=@{}
foreach ($item in $steps.GetEnumerator()) {
 $answer=Read-Host ($item.Value+' [実確認済みなら YES]')
 if ($answer -cne 'YES') { throw ('未確認Gate: '+$item.Key+'。PASS記録を作成しません。') }
 $checks[$item.Key]=$true
}
@{schema=1; repository=$build.repository; sourceCommit=$build.sourceCommit; ciRunId=$build.ciRunId; microsoftClientId=$settings.microsoftClientId; setupSha256=(Get-FileHash (Join-Path $assetsPath 'TtroClient-Setup.exe') -Algorithm SHA256).Hash.ToLowerInvariant(); portableSha256=(Get-FileHash (Join-Path $assetsPath 'TtroClient-Portable.zip') -Algorithm SHA256).Hash.ToLowerInvariant(); completedAtUtc=[DateTime]::UtcNow.ToString('o'); checks=$checks} | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $Output
Write-Host ('記録した証拠: '+$Output+'。Owner E2E evidence workflowへJSONを入力します。')
