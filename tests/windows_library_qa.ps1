param([string]$QaDll = (Join-Path $PSScriptRoot 'library-ui/bin/Release/net8.0-windows/Ttro.Library.Qa.dll'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$env:TTRO_LIBRARY_QA_ROOT = Join-Path $env:TEMP ('ttro-library-qa-' + [guid]::NewGuid().ToString('N'))
$process = Start-Process -FilePath 'dotnet' -ArgumentList ('"' + $QaDll + '"') -PassThru
$checks = @()
function Check([bool]$ok, [string]$name) { if (!$ok) { throw $name }; $script:checks += $name; Write-Host "PASS $name" }
function FindName([string]$name) { $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $name)) }
function FindId([string]$id) { $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)) }
function Click([string]$name) { $el = FindName $name; if (!$el) { throw "Control not found: $name" }; $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Section([string]$name) { (FindName $name).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
function WaitFor([scriptblock]$predicate, [string]$name) { for ($i=0; $i -lt 80; $i++) { if (& $predicate) { Check $true $name; return }; Start-Sleep -Milliseconds 150 }; throw "Timed out: $name" }
function State { Get-Content -Raw (Join-Path $env:TTRO_LIBRARY_QA_ROOT 'launcher.json') | ConvertFrom-Json }
function Screenshot([string]$name) { $bounds=$window.Current.BoundingRectangle; $bitmap=[System.Drawing.Bitmap]::new([int]$bounds.Width,[int]$bounds.Height); $g=[System.Drawing.Graphics]::FromImage($bitmap); try { $g.CopyFromScreen([int]$bounds.X,[int]$bounds.Y,0,0,$bitmap.Size); $bitmap.Save((Join-Path $PSScriptRoot "../research/$name.png")) } finally { $g.Dispose(); $bitmap.Dispose() } }
try {
 for ($i=0; $i -lt 60; $i++) { Start-Sleep -Milliseconds 200; $process.Refresh(); if ($process.HasExited) { Get-Content (Join-Path $env:TTRO_LIBRARY_QA_ROOT 'host-error.txt') -ErrorAction SilentlyContinue; throw 'QA host exited' }; if ($process.MainWindowHandle -ne 0) { break } }
 $window=[System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
 New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot '../research') | Out-Null
 WaitFor { $null -ne (FindName 'Install QA pack0') } 'Discover opens with compatible cards without search'
 WaitFor { (FindId 'ImageState').Current.Name -notlike '*: 0' } 'remote preview decoded and displayed before install'
 Check ($null -ne (FindName 'Details for QA pack0')) 'details independently reachable'
 Screenshot 'windows-library-discover'
 $button = FindName 'Install QA pack0'; $button.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
 WaitFor { (State).Profiles[0].Content.Count -eq 1 } 'single card keyboard activation installs managed pack'
 $state=State; $profile=$state.Profiles[0]; $file=$profile.Content[0].File
 Check ($profile.Packs[0] -eq $file) 'card install enables highest-priority pack'
 $options=Join-Path $env:TTRO_LIBRARY_QA_ROOT "profiles/$($profile.Id)/options.txt"
 Check ((Get-Content -Raw $options) -like "*$file*") 'Minecraft options updated by card install'
 WaitFor { $null -ne (FindName 'Installed QA pack0') } 'card becomes installed without second click'
 Section 'Installed content'
 WaitFor { $null -ne (FindName 'Enable QA pack0') } 'Installed exposes native enable control'
 $toggle=FindName 'Enable QA pack0'; $toggle.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait(' ')
 WaitFor { (State).Profiles[0].Packs.Count -eq 0 } 'keyboard disable persists'
 Click 'Use v2'; Click 'Check updates'
 WaitFor { $null -ne (FindName 'Update QA pack0') } 'managed update CTA appears'
 Click 'Update QA pack0'
 WaitFor { (State).Profiles[0].Content[0].VersionId -eq 'pack0v2' } 'card Update replaces managed version'
 Check ((State).Profiles[0].Packs.Count -eq 0) 'Update preserves disabled state'
 Check ((Get-ChildItem (Join-Path $env:TTRO_LIBRARY_QA_ROOT 'backups')).Count -ge 1) 'Update retains prior version backup'
 Click 'Offline'
 (FindName 'Enable QA pack0').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 WaitFor { (State).Profiles[0].Packs.Count -eq 1 } 'offline Installed enable works'
 Screenshot 'windows-library-installed'
 Section 'Discover content'
 WaitFor { (FindId 'LibraryStatus').Current.Name -like '*Unable to load Discover*' } 'offline Discover error with Retry'
 Check ($null -ne (FindName 'Retry')) 'offline Retry reachable'
 Click 'Online'; Click 'Retry'
 WaitFor { $null -ne (FindName 'Install QA pack1') } 'retry restores browse'
 $transform=$window.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
 $transform.Resize(620,620); Start-Sleep -Milliseconds 400
 $scrollEl=FindId 'LibraryScroll'; $scroll=$scrollEl.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
 function InstallVisible { $b=(FindName 'Install QA pack1').Current.BoundingRectangle; $v=$scrollEl.Current.BoundingRectangle; return $b.Height -gt 0 -and $b.Top -ge $v.Top -and $b.Bottom -le $v.Bottom -and $b.Left -ge $v.Left -and $b.Right -le $v.Right }
 for ($i=0; $i -lt 100 -and !(InstallVisible); $i++) { $scroll.Scroll([System.Windows.Automation.ScrollAmount]::NoAmount,[System.Windows.Automation.ScrollAmount]::SmallIncrement); Start-Sleep -Milliseconds 80 }
 Write-Host ('Narrow viewport: '+$scrollEl.Current.BoundingRectangle+'; INSTALL: '+(FindName 'Install QA pack1').Current.BoundingRectangle+'; scroll: '+$scroll.Current.VerticalScrollPercent)
 Check ((FindName 'Install QA pack1').Current.IsEnabled -and (InstallVisible)) 'narrow resize retains fully visible card install by viewport coordinates'
 Screenshot 'windows-library-narrow'
 $transform.Resize(960,760); Start-Sleep -Milliseconds 200
 (FindId 'ContentQuery').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('pack1')
 WaitFor { $null -ne (FindName 'Install QA pack10') } 'debounced search retrieves compatible cards'
 (FindName 'Favorite QA pack1').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 Section 'Favorite content'
 WaitFor { $null -ne (FindName 'Install QA pack1') } 'local Favorites retain card install'
 Section 'Discover content'
 (FindId 'ContentQuery').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('')
 WaitFor { $null -ne (FindName 'Install QA pack0') -or $null -ne (FindName 'Installed QA pack0') } 'clear query restores browse'
 Click 'Slow download'; Click 'Install QA pack1'
 WaitFor { (FindId 'CancelContent').Current.IsOffscreen -eq $false } 'install progress exposes cancellation'
 Click 'Cancel'
 WaitFor { (FindId 'LibraryStatus').Current.Name -like 'Cancelled*' } 'cancelled install returns usable state'
 Check ((State).Profiles[0].Content.Count -eq 1) 'cancel does not commit another pack'
 Click 'Fast download'
 Click 'Other profile'; Section 'Installed content'
 Check ((State).Profiles.Count -eq 2 -and (State).Profiles[1].Content.Count -eq 0) 'profile switch isolates installed library'
 $kind=FindId 'ContentKind'; $kind.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait('{END}{ENTER}'); Section 'Discover content'
 WaitFor { $null -ne (FindName 'Install QA mod0') } 'Mods browse opens with Forge cards'
 Click 'Install QA mod0'
 WaitFor { (State).Profiles[1].Content.Count -eq 1 -and (State).Profiles[1].Content[0].Kind -eq 'mod' } 'Mod card one-click installation persists'
 Section 'Installed content'
 (FindName 'Enable QA mod0').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 WaitFor { (State).Profiles[1].Content[0].File -like '*.disabled' } 'Mod disabled state persists from Installed'
 (FindName 'Enable QA mod0').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
 WaitFor { (State).Profiles[1].Content[0].File -like '*.jar' } 'Mod enabled state restores from Installed'
 $process.CloseMainWindow() | Out-Null; Check ($process.WaitForExit(5000)) 'Library closes cleanly'
 @{ environment='Windows UI Automation / real WPF Library / deterministic provider'; checks=$checks; status='PASS'; limits=@('Provider is a fixture, not live Modrinth','No Microsoft Login or Minecraft E2E','DPI/Narrator/physical input not certified') } | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $PSScriptRoot '../research/windows-library-smoke.json')
} finally { if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }; Remove-Item -Recurse -Force $env:TTRO_LIBRARY_QA_ROOT -ErrorAction SilentlyContinue }
