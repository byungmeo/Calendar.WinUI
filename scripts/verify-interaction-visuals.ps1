param([Parameter(Mandatory)][int]$AppPid, [string]$ArtifactDirectory)
$ErrorActionPreference = 'Stop'
if ($AppPid -le 0) { throw 'Pass the verified local example PID.' }
if (-not $ArtifactDirectory) {
    $ArtifactDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts\interaction-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null
$priorWorkflow = $env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID = [guid]::NewGuid().ToString('N')
function UI([string[]]$Arguments) {
    $output = @(& winapp ui @Arguments -a $AppPid)
    if ($LASTEXITCODE -ne 0) { throw "winapp ui $($Arguments -join ' ') failed: $($output -join ' ')" }
    $output
}
$report = [ordered]@{appPid=$AppPid; dateBorderChecks=@()}
try {
    UI @('invoke','ResetEvents') | Out-Null
    UI @('invoke','CustomTemplate','--action','toggle-off') | Out-Null
    UI @('invoke','DateSelection','--action','toggle-on') | Out-Null
    UI @('invoke','EventSelection','--action','toggle-on') | Out-Null
    UI @('invoke','CalendarDate_2026-10-10') | Out-Null
    Add-Type -AssemblyName System.Drawing
    foreach ($theme in @('light','dark')) {
        $action = if ($theme -eq 'dark') {'toggle-on'} else {'toggle-off'}
        UI @('invoke','DarkTheme','--action',$action) | Out-Null
        UI @('hover','CalendarDate_2026-10-01') | Out-Null
        $normalPath = Join-Path $ArtifactDirectory "$theme-date-normal.png"
        UI @('screenshot','CalendarDate_2026-10-10','--output',$normalPath,'--json') | Out-Null
        UI @('hover','CalendarDate_2026-10-10') | Out-Null
        $hoverPath = Join-Path $ArtifactDirectory "$theme-date-hover.png"
        UI @('screenshot','CalendarDate_2026-10-10','--output',$hoverPath,'--json') | Out-Null
        $normal = [Drawing.Bitmap]::new($normalPath)
        $hover = [Drawing.Bitmap]::new($hoverPath)
        try {
            if ($normal.Size -ne $hover.Size) { throw 'Hover changed the selected date dimensions.' }
            $different = 0
            $accentPixels = 0
            for ($x = 0; $x -lt $normal.Width; $x++) {
                for ($y = 0; $y -lt $normal.Height; $y++) {
                    if ($x -gt 0 -and $x -lt $normal.Width - 1 -and $y -gt 0 -and $y -lt $normal.Height - 1) { continue }
                    $pixel = $normal.GetPixel($x,$y)
                    if ($pixel.ToArgb() -ne $hover.GetPixel($x,$y).ToArgb()) { $different++ }
                    if ($pixel.B -gt $pixel.R + 30) { $accentPixels++ }
                }
            }
            if ($different -ne 0 -or $accentPixels -lt 20) { throw "Selected border mismatch: changed=$different; accent=$accentPixels" }
            $report.dateBorderChecks += @{theme=$theme;status='PASS';changedEdgePixels=$different;accentPixels=$accentPixels;normal=$normalPath;hover=$hoverPath}
        } finally { $normal.Dispose(); $hover.Dispose() }
    }
    UI @('invoke','DarkTheme','--action','toggle-off') | Out-Null
} finally {
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ArtifactDirectory 'results.json')
    winapp ui yield | Out-Null
    $env:WINAPP_UI_WORKFLOW_ID = $priorWorkflow
}
Write-Output "Results: $ArtifactDirectory"
