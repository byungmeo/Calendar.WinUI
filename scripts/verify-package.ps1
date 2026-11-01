param([string[]]$NetVersions = @('8.0','9.0','10.0'), [string]$PackageCache)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskProject = Join-Path $taskRoot 'samples\Calendar.Example\Calendar.Example.csproj'
if (-not $PackageCache) { $PackageCache = Join-Path $taskRoot 'artifacts/nuget/example' }
$PackageCache = [IO.Path]::GetFullPath($PackageCache)
[xml]$taskProjectXml = Get-Content -LiteralPath $taskProject -Raw
if ($taskProjectXml.SelectNodes('//ProjectReference').Count) { throw 'The example must consume packages without ProjectReference.' }
$taskDotnet = Join-Path $taskRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet = (Get-Command dotnet).Source }
$env:DOTNET_ROOT = Split-Path $taskDotnet -Parent
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$taskReportPath = Join-Path $taskRoot ('artifacts\package-smoke-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskReportPath -Force | Out-Null
$taskResults = [Collections.Generic.List[object]]::new()
$taskPreviousWorkflow = $env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID = [guid]::NewGuid().ToString('N')
try {
    & (Join-Path $PSScriptRoot 'build.ps1') -PackagesOnly -PackageCache $PackageCache
    if ($LASTEXITCODE -ne 0) { throw 'Local package preparation failed.' }
    foreach ($taskNet in $NetVersions) {
        $taskAppPid = 0
        try {
            $taskRestoreArgs = @("-p:RestorePackagesPath=$PackageCache", "-p:ExampleNetVersion=$taskNet", '-p:EnablePackageChecks=true')
            & $taskDotnet restore $taskProject @taskRestoreArgs --force --force-evaluate -v:minimal
            if ($LASTEXITCODE -ne 0) { throw "Example restore failed for $taskNet" }
            $taskAssets = Get-Content -LiteralPath (Join-Path (Split-Path $taskProject) 'obj/project.assets.json') -Raw | ConvertFrom-Json
            if (@($taskAssets.libraries.PSObject.Properties | Where-Object { $_.Name -like 'Calendar.Core/*' -or $_.Name -like 'Calendar.WinUI/*' }).Count) {
                throw 'The example still restores a legacy Calendar package ID.'
            }
            if (@($taskAssets.libraries.PSObject.Properties | Where-Object { $_.Name -like 'Byungmeo.Calendar.WinUI/*' -and $_.Value.type -eq 'package' }).Count -ne 1) {
                throw 'The example must restore the single Byungmeo.Calendar.WinUI package.'
            }
            & $taskDotnet build $taskProject @taskRestoreArgs --no-restore -v:minimal
            if ($LASTEXITCODE -ne 0) { throw "Consumer build failed for $taskNet" }
            $taskRunArgs = @('-p', "RestorePackagesPath=$PackageCache", '-p', "ExampleNetVersion=$taskNet", '-p', 'EnablePackageChecks=true')
            $taskLaunch = @(& winapp run $taskProject --arch x64 @taskRunArgs --no-build --no-restore --detach --json) | ConvertFrom-Json
            if ($LASTEXITCODE -ne 0 -or $taskLaunch.Error -or -not $taskLaunch.ProcessId) { throw "Consumer launch failed: $($taskLaunch | ConvertTo-Json -Compress)" }
            $taskAppPid = [int]$taskLaunch.ProcessId
            function ConsumerUI([string[]]$Arguments) {
                $taskOutput = @(& winapp ui @Arguments -a $taskAppPid)
                if ($LASTEXITCODE -ne 0) { throw "Consumer UI failed: $($Arguments -join ' ')" }
                $taskOutput
            }
            ConsumerUI @('wait-for','ApiRegressionStatus','--value','API checks: PASS','--contains','-t','20000') | Out-Null
            $taskApiChecks = ConsumerUI @('get-value','ApiRegressionStatus','--json') | ConvertFrom-Json
            ConsumerUI @('wait-for','CalendarMore_2026-10-28','-t','8000') | Out-Null
            ConsumerUI @('invoke','CalendarDate_2026-10-10') | Out-Null
            ConsumerUI @('wait-for','Status','--value','Date: 2026-10-10; outside=False','--contains','-t','3000') | Out-Null
            ConsumerUI @('invoke','CalendarEvent_4_1') | Out-Null
            ConsumerUI @('wait-for','Status','--value','dates=1; events=1','--contains','-t','3000') | Out-Null
            ConsumerUI @('invoke','CalendarMore_2026-10-28') | Out-Null
            ConsumerUI @('wait-for','Status','--value','More: 2026-10-28; all=11','--contains','-t','3000') | Out-Null
            $taskRuntime = ConsumerUI @('get-value','RuntimeVersion','--json') | ConvertFrom-Json
            if ($taskRuntime.text -notlike "*.NET $taskNet.*") { throw "Wrong runtime: $($taskRuntime.text)" }
            $taskShot = Join-Path $taskReportPath "net$taskNet.png"
            ConsumerUI @('screenshot','--output',$taskShot,'--json') | Out-Null
            if (-not (Test-Path -LiteralPath $taskShot)) { throw 'Screenshot was not delivered.' }
            $taskResults.Add([pscustomobject]@{target=$taskNet;status='PASS';runtime=$taskRuntime.text;apiChecks=$taskApiChecks.text;pid=$taskAppPid;screenshot=$taskShot})
        } catch {
            $taskResults.Add([pscustomobject]@{target=$taskNet;status='FAIL';detail="$_"})
        } finally {
            if ($taskAppPid -gt 0) {
                $taskProcess = Get-Process -Id $taskAppPid -ErrorAction SilentlyContinue
                if ($taskProcess) { $null = $taskProcess.CloseMainWindow(); if (-not $taskProcess.WaitForExit(3000)) { Stop-Process -Id $taskAppPid } }
            }
        }
    }
} finally {
    & winapp ui yield | Out-Null
    $env:WINAPP_UI_WORKFLOW_ID = $taskPreviousWorkflow
}
$taskResults | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskReportPath 'results.json') -Encoding utf8
$taskResults | Format-Table -Wrap
Write-Host "Results: $taskReportPath"
if (@($taskResults | Where-Object {$_.status -eq 'FAIL'}).Count) { exit 1 }
