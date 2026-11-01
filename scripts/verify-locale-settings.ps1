param([Parameter(Mandatory)][int]$AppPid,[Parameter(Mandatory)][string]$ArtifactDirectory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force -Path $ArtifactDirectory | Out-Null
$previousWorkflow=$env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID=[guid]::NewGuid().ToString('N')
$results=[Collections.Generic.List[object]]::new()
$screenshots=[Collections.Generic.List[string]]::new()
function UI([string[]]$Arguments) {
    $output=@(& winapp ui @Arguments -a $AppPid)
    if($LASTEXITCODE -ne 0){throw "winapp ui $($Arguments -join ' ') failed: $output"}
    $output
}
function Select-Choice([string]$Combo,[string]$Choice) {
    $comboMatch=UI @('search',$Combo,'--json') | ConvertFrom-Json
    $comboSelector=$comboMatch.matches[0].selector
    UI @('focus',$comboSelector,'-w',$script:mainHwnd) | Out-Null
    UI @('invoke',$comboSelector,'--action','expand') | Out-Null
    if($Combo -eq 'DisplayTimeZone') {
        $index=0
        if($Choice -ne 'TimeZoneSystem') {
            if($Choice -notmatch '^TimeZoneUTC_([PM])(\d{2})_(\d{2})$'){throw 'Invalid offset selector'}
            $minutes=([int]$Matches[2])*60+[int]$Matches[3]
            if($Matches[1] -eq 'M'){$minutes=-$minutes}
            $offsets=@([TimeZoneInfo]::GetSystemTimeZones().BaseUtcOffset + [TimeSpan]::Zero | Sort-Object -Unique)
            $index=-1
            for($i=0;$i -lt $offsets.Count;$i++){if($offsets[$i].TotalMinutes -eq $minutes){$index=$i+1;break}}
            if($index -lt 0){throw "Offset unavailable: $Choice"}
        }
        # Real keyboard navigation realizes ComboBox items outside its virtualized popup viewport.
        $keys=('home ' + ('down '*$index) + 'enter').Trim()
        # The popup is an owned HWND, while the application's main HWND retains foreground ownership.
        UI @('send-keys',$keys,'--via','send-input','-w',$script:mainHwnd) | Out-Null
        UI @('invoke',$comboSelector,'--action','collapse') | Out-Null
        return
    }
    $found=UI @('search',$Choice,'--json') | ConvertFrom-Json
    if(-not $found.matches.Count){throw "Missing choice $Choice"}
    UI @('invoke',$found.matches[0].selector,'--action','select') | Out-Null
    UI @('invoke',$comboSelector,'--action','collapse') | Out-Null
}
function Check([string]$Name,[scriptblock]$Action) {
    try{ & $Action | Out-Null; $results.Add(@{name=$Name;status='PASS'}) }
    catch{ $results.Add(@{name=$Name;status='FAIL';detail="$_"}) }
}
function Shot([string]$Name) {
    $path=Join-Path $ArtifactDirectory $Name
    UI @('screenshot','--output',$path,'--json') | Out-Null
    if(-not(Test-Path -LiteralPath $path)){throw 'Missing screenshot'}
    $screenshots.Add($path)
}
try {
    $mainWindow=@(UI @('list-windows','--json') | ConvertFrom-Json) | Where-Object title -eq 'Calendar Example' | Select-Object -First 1
    if(-not $mainWindow){throw 'Verified example window required.'}
    $script:mainHwnd=[string]$mainWindow.hwnd
    UI @('wait-for','DisplayLanguage','-t','5000') | Out-Null
    Check 'Both settings initially use system defaults' {
        $script:initialSystemLabels=@{}
        foreach($id in @('DisplayLanguage','DisplayTimeZone')) {
            $value=UI @('get-value',$id,'--json') | ConvertFrom-Json
            if($value.text -notmatch '^(System|시스템)'){throw "Unexpected default: $($value.text)"}
            $script:initialSystemLabels[$id]=$value.text
        }
        Shot '01-system.png'
    }
    Check 'Korean changes example chrome and calendar date resources immediately' {
        Select-Choice DisplayLanguage LanguageKorean
        UI @('wait-for','MonthTitle','--value','2026년 10월','-t','3000')
        UI @('wait-for','PreviousMonth','-p','Name','--value','이전','-t','3000')
        UI @('invoke','CalendarDate_2026-10-10')
        UI @('wait-for','CalendarDate_2026-10-10','-p','HelpText','--value','선택됨','-t','3000')
        UI @('wait-for','CalendarMore_2026-10-28','-p','Name','--value','더보기','--contains','-t','3000')
        Shot '02-korean.png'
    }
    Check 'English changes formatting, resources and selection text without losing selection' {
        Select-Choice DisplayLanguage LanguageEnglish
        UI @('wait-for','MonthTitle','--value','October 2026','-t','3000')
        UI @('wait-for','DisplayTimeZone','--value','System','--contains','-t','3000')
        UI @('wait-for','PreviousMonth','-p','Name','--value','Previous','-t','3000')
        UI @('wait-for','CalendarDate_2026-10-10','-p','Name','--value','Saturday, October 10, 2026','-t','3000')
        UI @('wait-for','CalendarDate_2026-10-10','-p','HelpText','--value','Selected','-t','3000')
        UI @('wait-for','CalendarMore_2026-10-28','-p','Name','--value','more','--contains','-t','3000')
        Shot '03-english.png'
    }
    Check 'UTC and UTC+09 move the absolute event while keeping all-day dates' {
        Select-Choice DisplayTimeZone TimeZoneUTC_P00_00
        UI @('invoke','CalendarEvent_3_13')
        UI @('wait-for','Status','--value','date=2026-10-20','--contains','-t','3000')
        Shot '04-utc.png'
        Select-Choice DisplayTimeZone TimeZoneUTC_P09_00
        UI @('invoke','CalendarEvent_3_13')
        UI @('wait-for','Status','--value','date=2026-10-21','--contains','-t','3000')
        UI @('invoke','CalendarEvent_4_1')
        UI @('wait-for','Status','--value','date=2026-10-26','--contains','-t','3000')
        Shot '05-utc-plus09.png'
    }
    Check 'Quarter-hour offsets and negative offsets can be selected' {
        Select-Choice DisplayTimeZone TimeZoneUTC_P05_45
        UI @('wait-for','DisplayTimeZone','--value','UTC+05:45','-t','3000')
        UI @('invoke','CalendarEvent_3_13')
        UI @('wait-for','Status','--value','date=2026-10-21','--contains','-t','3000')
        Select-Choice DisplayTimeZone TimeZoneUTC_M12_00
        UI @('invoke','CalendarEvent_3_13')
        UI @('wait-for','Status','--value','date=2026-10-20','--contains','-t','3000')
    }
    Check 'Today follows display zone and both settings can return to system' {
        Select-Choice DisplayTimeZone TimeZoneUTC_P14_00
        UI @('invoke','TodayMonth')
        $expected=[DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(14)).ToString('Y',[Globalization.CultureInfo]::GetCultureInfo('en-US'))
        UI @('wait-for','MonthTitle','--value',$expected,'-t','3000')
        Select-Choice DisplayTimeZone TimeZoneSystem
        Select-Choice DisplayLanguage LanguageSystem
        foreach($id in @('DisplayLanguage','DisplayTimeZone')) {
            $value=UI @('get-value',$id,'--json') | ConvertFrom-Json
            if($value.text -notmatch '^(System|시스템)'){throw 'System setting not restored'}
            if($value.text -ne $script:initialSystemLabels[$id]){throw 'System caption language not restored'}
        }
        UI @('invoke','ResetEvents')
        Shot '06-system-restored.png'
    }
} catch {$results.Add(@{name='Setup';status='FAIL';detail="$_"})}
finally {try{ & winapp ui yield | Out-Null }finally{$env:WINAPP_UI_WORKFLOW_ID=$previousWorkflow}}
$report=@{target='local';appPid=$AppPid;passed=@($results|Where-Object status -eq PASS).Count;failed=@($results|Where-Object status -eq FAIL).Count;results=$results.ToArray();screenshots=$screenshots.ToArray()}
$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'results.json')
$results|ForEach-Object{[pscustomobject]$_}|Format-Table -Wrap
if($report.failed){exit 1}
