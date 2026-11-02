param([Parameter(Mandatory)][int]$AppPid, [Parameter(Mandatory)][string]$ArtifactDirectory)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $ArtifactDirectory | Out-Null
$previousWorkflow = $env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID = [guid]::NewGuid().ToString('N')
$results = [Collections.Generic.List[object]]::new()
$screenshots = [Collections.Generic.List[string]]::new()
function UI([string[]]$Arguments) {
    $output = @(& winapp ui @Arguments -a $AppPid)
    if ($LASTEXITCODE -ne 0) { throw "winapp ui $($Arguments -join ' ') failed: $output" }
    $output
}
function Check([string]$Name, [scriptblock]$Action) {
    try { & $Action | Out-Null; $results.Add(@{name=$Name;status='PASS'}) }
    catch { $results.Add(@{name=$Name;status='FAIL';detail="$_"}) }
}
function Resize([int]$Width,[int]$Height) {
    if (-not [CalendarResizeRegression]::SetWindowPos([IntPtr][long]$script:hwnd,[IntPtr]::Zero,0,0,$Width,$Height,22)) { throw 'Resize failed.' }
    Start-Sleep -Milliseconds 150
}
function Selected-And-Focused {
    UI @('wait-for','CalendarDate_2026-10-10','-p','HelpText','--value','Selected','-t','2000')
    $focused = UI @('get-focused','--json') | ConvertFrom-Json
    if ($focused.element.automationId -ne 'CalendarDate_2026-10-10') { throw 'Resize lost the focused date.' }
    UI @('wait-for','CalendarEvent_4_1','-p','HelpText','--value','Selected','-t','2000')
}
function Screenshot([string]$Name) {
    $path = Join-Path $ArtifactDirectory $Name
    UI @('screenshot','--output',$path,'--json') | Out-Null
    if (-not (Test-Path -LiteralPath $path)) { throw 'Screenshot missing.' }
    $screenshots.Add($path)
}
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class CalendarResizeRegression {
    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}
'@
try {
    $main = @(UI @('list-windows','--json') | ConvertFrom-Json) | Where-Object {$_.title -eq 'Calendar Example'} | Select-Object -First 1
    if (-not $main) { throw 'Verified example window required.' }
    $script:hwnd = [string]$main.hwnd
    UI @('wait-for','ApiRegressionStatus','--value','UI diagnostics: ready','-t','5000') | Out-Null
    $languageCombo = UI @('search','DisplayLanguage','--json') | ConvertFrom-Json
    UI @('invoke', $languageCombo.matches[0].selector,'--action','expand') | Out-Null
    $languageMatch = UI @('search','LanguageEnglish','--json') | ConvertFrom-Json
    UI @('invoke', $languageMatch.matches[0].selector,'--action','select') | Out-Null
    UI @('invoke', $languageCombo.matches[0].selector,'--action','collapse') | Out-Null
    UI @('invoke','ResetEvents') | Out-Null
    UI @('invoke','CustomTemplate','--action','toggle-off') | Out-Null
    UI @('invoke','DarkTheme','--action','toggle-off') | Out-Null
    UI @('invoke','DateSelection','--action','toggle-on') | Out-Null
    UI @('invoke','EventSelection','--action','toggle-on') | Out-Null
    Resize 1200 1000
    UI @('invoke','CalendarEvent_4_1') | Out-Null
    UI @('invoke','CalendarDate_2026-10-10') | Out-Null
    UI @('focus','CalendarDate_2026-10-10') | Out-Null
    Check 'Width resize preserves date focus and independent event selection' {
        foreach ($width in @(1000,1150,950,1180,1200)) { Resize $width 1000; Selected-And-Focused }
        UI @('wait-for','Status','--value','dates=1; events=1','--contains','-t','2000')
    }
    Check 'Height resize within capacity preserves focus and selection' {
        foreach ($height in @(1005,1015,1000,1020,1000)) { Resize 1200 $height; Selected-And-Focused }
    }
    Check 'Capacity increase retains selection and focused date' {
        Resize 1200 1100
        Selected-And-Focused
        Screenshot '01-expanded-selected.png'
    }
    Check 'Collapsed overflow reports all events and expansion restores selected occurrence' {
        Resize 1200 820
        UI @('invoke','CalendarMore_2026-10-28')
        UI @('wait-for','Status','--value','More: 2026-10-28; all=11','--contains','-t','2000')
        Screenshot '02-collapsed-overflow.png'
        Resize 1200 1100
        UI @('wait-for','CalendarEvent_4_1','-p','HelpText','--value','Selected','-t','2000')
        UI @('invoke','CalendarEvent_4_1')
        UI @('wait-for','Status','--value','date=2026-10-26; outside=False; dates=1; events=2','--contains','-t','2000')
    }
    Check 'Reused more button reads updated source and event selection survives refresh' {
        UI @('invoke','CalendarDate_2026-10-28')
        UI @('invoke','AddEvent')
        UI @('invoke','CalendarMore_2026-10-28')
        UI @('wait-for','Status','--value','all=12','--contains','-t','2000')
        UI @('wait-for','CalendarEvent_4_1','-p','HelpText','--value','Selected','-t','2000')
    }
    Check 'Retained separators and event visuals update through Light Dark Light' {
        Resize 1200 1000
        Screenshot '03-light.png'
        UI @('invoke','DarkTheme','--action','toggle-on')
        Screenshot '04-dark.png'
        UI @('invoke','DarkTheme','--action','toggle-off')
        Screenshot '05-light-restored.png'
        UI @('wait-for','CalendarEvent_4_1','-p','HelpText','--value','Selected','-t','2000')
    }
    Check 'Arrow selection still immediately updates after resize' {
        UI @('focus','CalendarDate_2026-10-28')
        UI @('send-keys','right','--via','send-input')
        UI @('wait-for','Status','--value','Selected date: 2026-10-29; dates=2; events=2','--contains','-t','2000')
    }
} catch { $results.Add(@{name='Setup';status='FAIL';detail="$_"}) }
finally {
    try { if ($script:hwnd) { Resize 1200 1000 } }
    finally {
        try { & winapp ui yield | Out-Null }
        finally { $env:WINAPP_UI_WORKFLOW_ID = $previousWorkflow }
    }
}
$report = @{appPid=$AppPid;target='local';passed=@($results | Where-Object status -eq 'PASS').Count;failed=@($results | Where-Object status -eq 'FAIL').Count;results=$results.ToArray();screenshots=$screenshots.ToArray()}
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'results.json')
$results | ForEach-Object {[pscustomobject]$_} | Format-Table -Wrap
if ($report.failed) { exit 1 }
