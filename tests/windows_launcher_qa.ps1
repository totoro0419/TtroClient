param([string]$LauncherPath = (Join-Path $PSScriptRoot '../dist/portable/TtroClient.exe'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$exe = $LauncherPath
$process = Start-Process -FilePath $exe -PassThru
$checks = @()
function Check([bool]$ok, [string]$name) { if (!$ok) { throw $name }; $script:checks += $name; Write-Host "PASS $name" }
function FindId([string]$id) { $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)) }
function FindName([string]$name) { $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $name)) }
try {
 for ($i=0; $i -lt 30; $i++) { Start-Sleep -Milliseconds 300; $process.Refresh(); if ($process.HasExited) { throw 'Launcher exited at startup' }; if ($process.MainWindowHandle -ne 0) { break } }
 Check ($process.MainWindowHandle -ne 0) 'native main window opens'
 $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
 Check ($window.Current.Name -eq 'Ttro Client') 'window branding'
 Check ($null -ne (FindId 'PlayButton')) 'PLAY accessible control'
 $transform = $window.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
 $transform.Resize(760,620); Start-Sleep -Milliseconds 500
 Check ($window.Current.BoundingRectangle.Width -ge 620) 'window resize preserves minimum layout'
 $config = Get-Content -Raw (Join-Path (Split-Path $exe) 'launcher-settings.json') | ConvertFrom-Json
 if ([string]::IsNullOrWhiteSpace($config.microsoftClientId)) {
  $play = FindId 'PlayButton'; $play.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
  Start-Sleep -Milliseconds 700
  $status = FindId 'Status'
  Check ($status.Current.Name -like '*registered Ttro Client application ID*') 'missing OAuth configuration is visible, no offline success'
  Check ((FindId 'Workspace').Current.IsEnabled) 'UI recovers after authentication error'
 } else {
  Check ([guid]$config.microsoftClientId -ne [guid]::Empty) 'packaged public client ID is configured'
  Check ((FindId 'LoginButton').Current.IsEnabled) 'configured sign-in control is available; actual login requires owner QA'
 }
 $library = FindName 'LIBRARY'; $library.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 $expander = FindName 'Profile and performance'; $expander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
 (FindId 'ProfileName').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Windows CI QA')
 (FindName 'Create profile').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 500
 $data = Join-Path $env:LOCALAPPDATA 'TtroClient189/native/launcher.json'
 $state = Get-Content -Raw $data | ConvertFrom-Json
 Check ($null -ne ($state.Profiles | Where-Object Name -eq 'Windows CI QA')) 'native UI creates persistent profile'
 $tune = FindName 'TUNE'; $tune.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
 (FindId 'ModuleSearch').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Crosshair')
 Start-Sleep -Milliseconds 300
 $toggle = FindName 'Enable Crosshair'; Check ($null -ne $toggle) 'context module exposes native automation semantics'
 $toggle.SetFocus()
 [System.Windows.Forms.SendKeys]::SendWait(' ')
 Start-Sleep -Milliseconds 300
 Check ($toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq [System.Windows.Automation.ToggleState]::Off) 'keyboard Space toggles native module'
 # Ctrl+K must reach the purpose search from the context controls.
 [System.Windows.Forms.SendKeys]::SendWait('^k')
 Start-Sleep -Milliseconds 300
 Check ((FindId 'ModuleSearch').Current.HasKeyboardFocus) 'keyboard Ctrl+K reaches module search'
 Start-Sleep -Milliseconds 300
 $state = Get-Content -Raw $data | ConvertFrom-Json
 $config = Get-Content -Raw (Join-Path $env:LOCALAPPDATA "TtroClient189/native/profiles/$($state.Selected)/config/ttro-client.json") | ConvertFrom-Json
 Check (!$config.modules.crosshair.enabled) 'native module toggle persists OFF'
 Check ((FindId 'ModuleStateLabel').Current.Name -like 'OFF*') 'context state agrees with saved module state'
 New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot '../research') | Out-Null
 $bounds = $window.Current.BoundingRectangle
 $bitmap = [System.Drawing.Bitmap]::new([int]$bounds.Width,[int]$bounds.Height)
 $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
 try { $graphics.CopyFromScreen([int]$bounds.X,[int]$bounds.Y,0,0,$bitmap.Size); $bitmap.Save((Join-Path $PSScriptRoot '../research/windows-context.png')) } finally { $graphics.Dispose(); $bitmap.Dispose() }
 $process.CloseMainWindow() | Out-Null
 Check ($process.WaitForExit(5000)) 'native launcher closes normally'
 New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot '../research') | Out-Null
 @{ environment='GitHub Actions Windows runner / UI Automation'; checks=$checks; status='PASS'; limits=@('No Microsoft account sign-in', 'No Minecraft PLAY', 'No physical mouse, DPI or screen-reader certification') } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $PSScriptRoot '../research/windows-ui-smoke.json')
} finally { if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() } }
