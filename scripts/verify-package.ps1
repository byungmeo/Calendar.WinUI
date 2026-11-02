param(
    [ValidateSet('8.0','9.0','10.0')][string[]]$NetVersions = @('8.0','9.0','10.0'),
    [ValidatePattern('^2\.\d+\.\d+$')][string[]]$WindowsAppSdkVersions = @('2.0.1','2.6.4'),
    [string]$PackageCache
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskProject = Join-Path $taskRoot 'samples/Calendar.Example/Calendar.Example.csproj'
if (-not $PackageCache) { $PackageCache = Join-Path $taskRoot 'artifacts/nuget/example' }
$PackageCache = [IO.Path]::GetFullPath($PackageCache)
[xml]$taskProjectXml = Get-Content -LiteralPath $taskProject -Raw
if ($taskProjectXml.SelectNodes('//ProjectReference').Count) { throw 'The example must consume packages without ProjectReference.' }
$taskDotnet = Join-Path $taskRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet = (Get-Command dotnet).Source }
$env:DOTNET_ROOT = Split-Path $taskDotnet -Parent
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$taskReportPath = Join-Path $taskRoot ('artifacts/package-smoke-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskReportPath -Force | Out-Null
$taskResults = [Collections.Generic.List[object]]::new()
$taskPreviousWorkflow = $env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID = [guid]::NewGuid().ToString('N')
try {
    & (Join-Path $PSScriptRoot 'build.ps1') -PackagesOnly -PackageCache $PackageCache
    if ($LASTEXITCODE -ne 0) { throw 'Local package preparation failed.' }
    foreach ($taskSdk in $WindowsAppSdkVersions) {
        foreach ($taskNet in $NetVersions) {
            $taskAppPid = 0
            $taskCasePath = Join-Path $taskReportPath "appsdk-$taskSdk-net$taskNet"
            $taskLayout = [IO.Path]::GetFullPath((Join-Path $taskCasePath 'AppX'))
            New-Item -ItemType Directory -Path $taskCasePath -Force | Out-Null
            try {
                # A framework-dependent app can load a newer installed runtime and conceal compatibility failures.
                $taskProperties = @('Platform=x64', "RestorePackagesPath=$PackageCache", "ExampleNetVersion=$taskNet",
                    "ExampleWindowsAppSdkVersion=$taskSdk", 'WindowsAppSDKSelfContained=true',
                    'EnablePackageChecks=true', 'EnableUiDiagnostics=false',
                    "IntermediateOutputPath=obj/compat/appsdk-$taskSdk/net$taskNet/",
                    "OutputPath=bin/compat/appsdk-$taskSdk/net$taskNet/")
                $taskBuildArgs = @($taskProperties | ForEach-Object { "-p:$_" })
                & $taskDotnet restore $taskProject @taskBuildArgs --force --force-evaluate -v:minimal
                if ($LASTEXITCODE -ne 0) { throw "Example restore failed for .NET $taskNet / App SDK $taskSdk" }
                $taskAssets = Get-Content -LiteralPath (Join-Path (Split-Path $taskProject) 'obj/project.assets.json') -Raw | ConvertFrom-Json
                $taskLibraries = @($taskAssets.libraries.PSObject.Properties)
                if (@($taskLibraries | Where-Object { $_.Name -like 'Calendar.Core/*' -or $_.Name -like 'Calendar.WinUI/*' }).Count) {
                    throw 'The example still restores a legacy Calendar package ID.'
                }
                if (@($taskLibraries | Where-Object { $_.Name -like 'Byungmeo.Calendar.WinUI/*' -and $_.Value.type -eq 'package' }).Count -ne 1) {
                    throw 'The example must restore the single Byungmeo.Calendar.WinUI package.'
                }
                foreach ($taskId in @('Microsoft.WindowsAppSDK','Microsoft.WindowsAppSDK.Runtime')) {
                    $taskResolved = @($taskLibraries | Where-Object Name -like "$taskId/*")
                    if ($taskResolved.Count -ne 1 -or $taskResolved[0].Name -ne "$taskId/$taskSdk") {
                        throw "Unexpected restored version: $taskId; expected $taskSdk."
                    }
                }
                $taskLibraries | Where-Object Name -like 'Microsoft.WindowsAppSDK*' | Select-Object Name |
                    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskCasePath 'resolved-appsdk.json')
                & $taskDotnet build $taskProject @taskBuildArgs --no-restore -v:minimal
                if ($LASTEXITCODE -ne 0) { throw "Consumer build failed for .NET $taskNet / App SDK $taskSdk" }
                $taskRunArgs = @($taskProperties | ForEach-Object { '-p'; $_ })
                $taskLaunch = @(& winapp run $taskProject --arch x64 @taskRunArgs --no-build --no-restore --output-appx-directory $taskLayout --detach --json) | ConvertFrom-Json
                if ($LASTEXITCODE -ne 0 -or $taskLaunch.Error -or -not $taskLaunch.ProcessId) { throw "Consumer launch failed: $($taskLaunch | ConvertTo-Json -Compress)" }
                $taskAppPid = [int]$taskLaunch.ProcessId
                function ConsumerUI([string[]]$Arguments) {
                    $taskOutput = @(& winapp ui @Arguments -a $taskAppPid)
                    if ($LASTEXITCODE -ne 0) { throw "Consumer UI failed: $($Arguments -join ' ')" }
                    $taskOutput
                }
                ConsumerUI @('wait-for','ApiRegressionStatus','--value','API checks: PASS','--contains','-t','20000') | Out-Null
                $taskApiChecks = ConsumerUI @('get-value','ApiRegressionStatus','--json') | ConvertFrom-Json
                $taskModule = (Get-Process -Id $taskAppPid).Modules | Where-Object ModuleName -ieq 'Microsoft.UI.Xaml.dll' | Select-Object -First 1
                if (-not $taskModule -or -not $taskModule.FileName.StartsWith($taskLayout + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'The process did not load the app-local WinUI runtime.'
                }
                $taskRuntimeMsix = Join-Path $PackageCache "microsoft.windowsappsdk.runtime/$taskSdk/tools/MSIX/win10-x64/Microsoft.WindowsAppRuntime.2.msix"
                $taskRuntimeArchive = [IO.Compression.ZipFile]::OpenRead($taskRuntimeMsix)
                try {
                    $taskXamlEntry = $taskRuntimeArchive.Entries | Where-Object Name -ieq 'Microsoft.UI.Xaml.dll' | Select-Object -First 1
                    if (-not $taskXamlEntry) { throw 'The expected runtime MSIX has no WinUI native DLL.' }
                    $taskStream = $taskXamlEntry.Open()
                    try { $taskExpectedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskStream)) }
                    finally { $taskStream.Dispose() }
                } finally { $taskRuntimeArchive.Dispose() }
                $taskLoadedHash = (Get-FileHash -LiteralPath $taskModule.FileName -Algorithm SHA256).Hash
                if ($taskLoadedHash -ne $taskExpectedHash) { throw 'The loaded WinUI DLL does not match the requested runtime package.' }
                ConsumerUI @('wait-for','CalendarMore_2026-10-28','-t','8000') | Out-Null
                ConsumerUI @('invoke','CalendarDate_2026-10-10') | Out-Null
                ConsumerUI @('wait-for','Status','--value','Date: 2026-10-10; outside=False','--contains','-t','3000') | Out-Null
                ConsumerUI @('invoke','CalendarEvent_4_1') | Out-Null
                ConsumerUI @('wait-for','Status','--value','dates=1; events=1','--contains','-t','3000') | Out-Null
                ConsumerUI @('invoke','CalendarMore_2026-10-28') | Out-Null
                ConsumerUI @('wait-for','Status','--value','More: 2026-10-28; all=11','--contains','-t','3000') | Out-Null
                $taskRuntime = ConsumerUI @('get-value','RuntimeVersion','--json') | ConvertFrom-Json
                if ($taskRuntime.text -notlike "*.NET $taskNet.*") { throw "Wrong .NET runtime: $($taskRuntime.text)" }
                $taskShot = Join-Path $taskCasePath 'complete.png'
                ConsumerUI @('screenshot','--output',$taskShot,'--json') | Out-Null
                if (-not (Test-Path -LiteralPath $taskShot)) { throw 'Screenshot was not delivered.' }
                $taskResults.Add([pscustomobject]@{target=$taskNet;windowsAppSdk=$taskSdk;status='PASS';runtime=$taskRuntime.text;
                    apiChecks=$taskApiChecks.text;pid=$taskAppPid;winUiPath=$taskModule.FileName;
                    winUiFileVersion=$taskModule.FileVersionInfo.FileVersion;winUiSha256=$taskLoadedHash;runtimePackageHashMatched=$true;screenshot=$taskShot})
            } catch {
                $taskResults.Add([pscustomobject]@{target=$taskNet;windowsAppSdk=$taskSdk;status='FAIL';detail="$_"})
            } finally {
                if ($taskAppPid -gt 0) {
                    $taskProcess = Get-Process -Id $taskAppPid -ErrorAction SilentlyContinue
                    if ($taskProcess) { $null = $taskProcess.CloseMainWindow(); if (-not $taskProcess.WaitForExit(3000)) { Stop-Process -Id $taskAppPid } }
                }
            }
            $taskResults.ToArray() | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskReportPath 'results.json') -Encoding utf8
        }
    }
} finally {
    & winapp ui yield | Out-Null
    $env:WINAPP_UI_WORKFLOW_ID = $taskPreviousWorkflow
}
$taskResults | Format-Table target,windowsAppSdk,status,apiChecks,detail -Wrap
Write-Host "Results: $taskReportPath"
if (@($taskResults | Where-Object {$_.status -eq 'FAIL'}).Count) { exit 1 }
