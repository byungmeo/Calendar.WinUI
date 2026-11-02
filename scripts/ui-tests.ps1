param([int]$AppPid, [ValidateSet('local','sandbox')][string]$Target='local', [string]$ArtifactDirectory)
$ErrorActionPreference = 'Stop'
if ($AppPid -le 0) { throw 'Pass the verified PID from winapp run.' }
if (-not $ArtifactDirectory) { $ArtifactDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts\ui-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null
$scopeArgs = @(if ($Target -eq 'sandbox') { '--on'; 'sandbox' })
$previousWorkflow = $env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID = [guid]::NewGuid().ToString('N')
$results = [Collections.Generic.List[object]]::new()
$screenshots = [Collections.Generic.List[string]]::new()
function UI([string[]]$Arguments) {
    $output = @(& winapp ui @Arguments @scopeArgs -a $AppPid)
    if ($LASTEXITCODE -ne 0) { throw "winapp ui $($Arguments -join ' ') failed: $($output -join ' ')" }
    $output
}
function Test-UI([string]$Name,[scriptblock]$Action) {
    try { & $Action | Out-Null; $results.Add(@{name=$Name;status='PASS'}) }
    catch { $results.Add(@{name=$Name;status='FAIL';detail="$_"}) }
}
function Expect([string]$Value) {
    try { UI @('wait-for','Status','--value',$Value,'--contains','-t','4000') }
    catch {
        $actual = UI @('get-value','Status','--json') | ConvertFrom-Json
        throw "Expected status containing '$Value'; actual '$($actual.text)'."
    }
}
function Screenshot([string]$Name) {
    $path = Join-Path $ArtifactDirectory $Name
    $output = @(& winapp ui screenshot @scopeArgs -w $script:hwnd --output $path --json)
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $path)) { throw "Screenshot failed: $output" }
    $screenshots.Add($path)
}
try {
    $windows = @(UI @('list-windows','--json') | ConvertFrom-Json)
    $main = $windows | Where-Object {$_.title -eq 'Calendar Example'} | Select-Object -First 1
    if (-not $main) { throw 'No live Calendar Example window.' }
    $script:hwnd = [string]$main.hwnd
    UI @('wait-for','ApiRegressionStatus','--value','UI diagnostics: ready','-t','5000') | Out-Null
    $languageCombo = UI @('search','DisplayLanguage','--json') | ConvertFrom-Json
    UI @('invoke', $languageCombo.matches[0].selector,'--action','expand') | Out-Null
    $languageMatch = UI @('search','LanguageEnglish','--json') | ConvertFrom-Json
    UI @('invoke', $languageMatch.matches[0].selector,'--action','select') | Out-Null
    UI @('invoke', $languageCombo.matches[0].selector,'--action','collapse') | Out-Null
    UI @('invoke','ResetEvents') | Out-Null
    Test-UI 'Automatic five-week month and identified application controls' {
        UI @('wait-for','DateSelection','--value','On','-t','3000')
        UI @('wait-for','EventSelection','--value','On','-t','3000')
        UI @('wait-for','CalendarEvent_4_1','-p','IsEnabled','--value','True','-t','3000')
        $tree = UI @('inspect','--interactive','--json') | ConvertFrom-Json
        $elements = @($tree.windows | ForEach-Object {$_.elements})
        if (@($elements | Where-Object {$_.automationId -like 'CalendarDate_*'}).Count -ne 35) { throw 'Expected 35 date buttons.' }
        $calendarButtons = @($elements | Where-Object {$_.className -eq 'Button'})
        if (-not $calendarButtons.Count -or @($calendarButtons | Where-Object {-not $_.automationId -or -not $_.name}).Count) { throw 'Missing accessible button identity.' }
        Screenshot '01-month.png'
    }
    Test-UI 'Same date can be invoked repeatedly' {
        UI @('invoke','CalendarDate_2026-10-10')
        Expect 'dates=1; events=0'
        UI @('invoke','CalendarDate_2026-10-10')
        Expect 'dates=2; events=0'
        Screenshot '02-selected.png'
    }
    Test-UI 'Adjacent date invokes without navigating' {
        UI @('invoke','CalendarDate_2026-09-27')
        Expect 'Date: 2026-09-27; outside=True'
        UI @('wait-for','CalendarDate_2026-10-31','-t','3000')
    }
    Test-UI 'Event invocation does not invoke date' {
        UI @('invoke','ResetEvents')
        UI @('invoke','CalendarEvent_4_1')
        Expect 'dates=0; events=1'
    }
    Test-UI 'More returns all 11 occurrences without another invocation' {
        UI @('invoke','CalendarMore_2026-10-28')
        Expect 'More: 2026-10-28; all=11; outside=False; dates=0; events=1'
    }
    Test-UI 'Continuation in next month retains original occurrence' {
        UI @('invoke','NextMonth')
        UI @('wait-for','CalendarEvent_0_1','-t','3000')
        UI @('invoke','CalendarEvent_0_1')
        Expect 'date=2026-11-01; outside=False; dates=0; events=2'
        Screenshot '03-next-month.png'
        UI @('invoke','PreviousMonth')
        UI @('wait-for','CalendarDate_2026-09-27','-t','3000')
    }
    Test-UI 'Custom application model adapter' {
        UI @('invoke','CalendarEvent_2_12')
        Expect 'Custom application model'
        Expect 'dates=0; events=3'
    }
    Test-UI 'Arrow keys immediately select relative to the selected date without invocation' {
        UI @('invoke','ResetEvents')
        UI @('invoke','CalendarDate_2026-10-10')
        UI @('focus','CalendarDate_2026-10-10')
        UI @('send-keys','right','--via','send-input')
        Expect 'Selected date: 2026-10-11; dates=1; events=0'
        $focused = UI @('get-focused','--json') | ConvertFrom-Json
        if ($focused.element.automationId -ne 'CalendarDate_2026-10-11') { throw 'Input target did not follow the selected date.' }
        UI @('send-keys','up','--via','send-input')
        Expect 'Selected date: 2026-10-04; dates=1; events=0'
        UI @('send-keys','left','--via','send-input')
        Expect 'Selected date: 2026-10-03; dates=1; events=0'
        UI @('send-keys','down','--via','send-input')
        Expect 'Selected date: 2026-10-10; dates=1; events=0'
        UI @('wait-for','CalendarDate_2026-10-10','-p','HelpText','--value','Selected','-t','3000')
        Screenshot '02b-arrow-selected.png'
        UI @('send-keys','enter','--via','send-input')
        Expect 'Date: 2026-10-10; outside=False; dates=2; events=0'
    }
    Test-UI 'Arrow selection reaches adjacent dates and stops at visible boundaries' {
        UI @('invoke','ResetEvents')
        UI @('invoke','CalendarDate_2026-09-27')
        UI @('focus','CalendarDate_2026-09-27')
        UI @('send-keys','left','--via','send-input')
        Expect 'Date: 2026-09-27; outside=True; dates=1; events=0'
        UI @('send-keys','right','--via','send-input')
        Expect 'Selected date: 2026-09-28; dates=1; events=0'
        UI @('send-keys','up','--via','send-input')
        Expect 'Selected date: 2026-09-28; dates=1; events=0'
        UI @('invoke','CalendarDate_2026-10-31')
        UI @('focus','CalendarDate_2026-10-31')
        UI @('send-keys','right','--via','send-input')
        UI @('send-keys','down','--via','send-input')
        Expect 'Date: 2026-10-31; outside=False; dates=2; events=0'
    }
    Test-UI 'Observable collection change updates more payload' {
        UI @('invoke','CalendarDate_2026-10-28')
        UI @('invoke','AddEvent')
        UI @('invoke','CalendarMore_2026-10-28')
        Expect 'More: 2026-10-28; all=12'
    }
    Test-UI 'Nested template action does not invoke its parent event' {
        UI @('invoke','ResetEvents')
        UI @('invoke','CustomTemplate','--action','toggle-on')
        UI @('wait-for','TemplateAction_1_2026-10-26','-t','3000')
        UI @('invoke','TemplateAction_1_2026-10-26')
        Expect 'TemplateAction:'
        Expect 'dates=0; events=0'
        Screenshot '04-template.png'
    }
    Test-UI 'Dark theme' {
        UI @('invoke','DarkTheme','--action','toggle-on')
        Screenshot '05-dark.png'
    }
    Test-UI 'Selection can be disabled while invocation remains enabled' {
        UI @('invoke','DateSelection','--action','toggle-off')
        UI @('invoke','CalendarDate_2026-10-12')
        Expect 'Date: 2026-10-12; outside=False'
        UI @('focus','CalendarDate_2026-10-12')
        UI @('send-keys','right','--via','send-input')
        Expect 'Date: 2026-10-12; outside=False'
        $focused = UI @('get-focused','--json') | ConvertFrom-Json
        if ($focused.element.automationId -ne 'CalendarDate_2026-10-12') { throw 'Disabled date selection moved to an intermediate focus date.' }
    }
    Test-UI 'External navigation changes range and returns' {
        UI @('invoke','NextMonth')
        UI @('wait-for','CalendarDate_2026-12-05','-t','3000')
        UI @('invoke','PreviousMonth')
        UI @('wait-for','CalendarDate_2026-09-27','-t','3000')
    }
    Test-UI 'A naturally six-week month switches back to five weeks' {
        UI @('invoke','PreviousMonth')
        UI @('invoke','PreviousMonth')
        UI @('wait-for','CalendarDate_2026-09-05','-t','3000')
        $tree = UI @('inspect','--interactive','--json') | ConvertFrom-Json
        if (@($tree.windows.elements | Where-Object {$_.automationId -like 'CalendarDate_*'}).Count -ne 42) { throw 'August must show six weeks.' }
        Screenshot '06-auto-six.png'
        UI @('invoke','NextMonth')
        UI @('wait-for','CalendarDate_2026-10-03','-t','3000')
        $tree = UI @('inspect','--interactive','--json') | ConvertFrom-Json
        if (@($tree.windows.elements | Where-Object {$_.automationId -like 'CalendarDate_*'}).Count -ne 35) { throw 'September must show five weeks.' }
        UI @('invoke','NextMonth')
    }
    Test-UI 'Compact layout recalculates overflow and restores size' {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class CalendarUiWindow {
    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
'@
        try {
            if (-not [CalendarUiWindow]::SetWindowPos([IntPtr][long]$script:hwnd,[IntPtr]::Zero,0,0,900,720,6)) { throw 'Resize failed.' }
            UI @('wait-for','CalendarMore_2026-10-28','-t','3000')
            UI @('invoke','CalendarMore_2026-10-28')
            Expect 'More: 2026-10-28; all=11'
            Screenshot '07-compact.png'
        } finally { $null = [CalendarUiWindow]::SetWindowPos([IntPtr][long]$script:hwnd,[IntPtr]::Zero,0,0,1200,1000,6) }
    }
    Test-UI 'Event selection can be disabled without blocking invocation or nested actions' {
        UI @('invoke','ResetEvents')
        UI @('invoke','DateSelection','--action','toggle-on')
        UI @('invoke','EventSelection','--action','toggle-off')
        UI @('wait-for','CalendarEvent_4_1','-p','IsEnabled','--value','True','-t','3000')
        UI @('wait-for','TemplateAction_1_2026-10-26','-p','IsEnabled','--value','True','-t','3000')
        UI @('invoke','CalendarEvent_4_1')
        Expect 'dates=0; events=1'
        UI @('wait-for','EventSelectionStatus','--value','changes=0','--contains','-t','3000')
        UI @('invoke','TemplateAction_1_2026-10-26')
        Expect 'TemplateAction:'
        Expect 'dates=0; events=1'
        UI @('invoke','CalendarDate_2026-10-10')
        Expect 'Date: 2026-10-10; outside=False; dates=1; events=1'
        UI @('invoke','CalendarMore_2026-10-28')
        Expect 'More: 2026-10-28; all=11; outside=False; dates=1; events=1'
        Screenshot '08-events-disabled.png'
        UI @('invoke','DarkTheme','--action','toggle-off')
        Screenshot '08b-events-disabled-light.png'
        UI @('invoke','DarkTheme','--action','toggle-on')
    }
    Test-UI 'Event selection can be restored independently of date selection' {
        UI @('invoke','DateSelection','--action','toggle-off')
        UI @('invoke','EventSelection','--action','toggle-on')
        UI @('wait-for','CalendarEvent_4_1','-p','IsEnabled','--value','True','-t','3000')
        UI @('invoke','CalendarEvent_4_1')
        Expect 'dates=1; events=2'
        UI @('invoke','CalendarDate_2026-10-12')
        Expect 'Date: 2026-10-12; outside=False; dates=2; events=2'
        Screenshot '09-events-enabled.png'
    }
    Test-UI 'Event invocation can be disabled while selection remains enabled' {
        UI @('invoke','ResetEvents')
        UI @('invoke','EventInvocation','--action','toggle-off')
        UI @('invoke','CalendarEvent_4_1')
        Expect 'Reset'
        UI @('wait-for','EventSelectionStatus','--value','Selected event: Multi-day event: into next month; changes=1','-t','3000')
        UI @('invoke','EventInvocation','--action','toggle-on')
    }
    Test-UI 'Event selection persists independently of focus and repeated invocation' {
        UI @('invoke','ResetEvents')
        UI @('invoke','CustomTemplate','--action','toggle-off')
        UI @('invoke','DarkTheme','--action','toggle-off')
        UI @('invoke','DateSelection','--action','toggle-on')
        UI @('invoke','CalendarEvent_4_1')
        UI @('wait-for','EventSelectionStatus','--value','Selected event: Multi-day event: into next month; changes=1','-t','3000')
        UI @('invoke','CalendarDate_2026-10-10')
        UI @('focus','CalendarDate_2026-10-10')
        UI @('wait-for','CalendarEvent_4_1','-p','HelpText','--value','Selected','-t','3000')
        Screenshot '10-event-selected-light.png'
        UI @('invoke','CalendarEvent_4_1')
        UI @('wait-for','EventSelectionStatus','--value','changes=1','--contains','-t','3000')
        UI @('invoke','CalendarEvent_4_2')
        UI @('wait-for','EventSelectionStatus','--value','Selected event: Release preparation; changes=2','-t','3000')
    }
    Test-UI 'Light hover keeps event colors and does not create a tooltip' {
        UI @('hover','CalendarEvent_4_2','--dwell-time','1600')
        $tree = UI @('inspect','--json') | ConvertFrom-Json
        if (@($tree.windows.elements | Where-Object {$_.type -eq 'ToolTip' -or $_.className -eq 'ToolTip'}).Count) { throw 'Unexpected built-in event tooltip.' }
        Screenshot '11-event-hover-light.png'
    }
    Test-UI 'Keyboard selection and theme changes preserve the selected occurrence' {
        UI @('focus','CalendarEvent_4_1')
        UI @('send-keys','enter','--via','send-input')
        UI @('wait-for','EventSelectionStatus','--value','Selected event: Multi-day event: into next month; changes=3','-t','3000')
        Screenshot '12-event-keyboard-selected.png'
        UI @('invoke','DarkTheme','--action','toggle-on')
        UI @('hover','CalendarEvent_4_1','--dwell-time','1600')
        Screenshot '13-event-hover-dark.png'
    }
    Test-UI 'Continuation and source refresh preserve event selection' {
        UI @('invoke','NextMonth')
        UI @('wait-for','CalendarEvent_0_1','-p','HelpText','--value','Selected','-t','3000')
        Screenshot '14-selected-continuation.png'
        UI @('invoke','PreviousMonth')
        UI @('wait-for','CalendarEvent_4_1','-p','HelpText','--value','Selected','-t','3000')
        UI @('invoke','AddEvent')
        UI @('wait-for','EventSelectionStatus','--value','Selected event: Multi-day event: into next month; changes=3','-t','3000')
        UI @('wait-for','CalendarEvent_4_1','-p','HelpText','--value','Selected','-t','3000')
    }
} catch { $results.Add(@{name='Setup';status='FAIL';detail="$_"}) }
finally {
    & winapp ui yield @scopeArgs | Out-Null
    $env:WINAPP_UI_WORKFLOW_ID = $previousWorkflow
}
$failed = @($results | Where-Object {$_.status -eq 'FAIL'}).Count
$report = @{target=$Target;appPid=$AppPid;passed=$results.Count-$failed;failed=$failed;results=$results.ToArray();screenshots=$screenshots.ToArray()}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'results.json') -Encoding utf8
$results | ForEach-Object { [pscustomobject]$_ } | Format-Table name,status,detail -Wrap
Write-Host "Results: $ArtifactDirectory"
if ($failed) { exit 1 }
