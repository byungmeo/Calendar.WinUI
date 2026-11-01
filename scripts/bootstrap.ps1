param([switch]$IncludeTestRuntimes)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskTools = Join-Path $taskRoot '.tools'
New-Item -ItemType Directory -Path $taskTools -Force | Out-Null
$taskInstaller = Join-Path $taskTools 'dotnet-install.ps1'
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $taskInstaller
$taskSdk = Join-Path $taskTools 'dotnet'
& $taskInstaller -Version '10.0.403' -InstallDir $taskSdk -NoPath
if ($IncludeTestRuntimes) {
    & $taskInstaller -Runtime dotnet -Version '8.0.33' -InstallDir $taskSdk -NoPath
    & $taskInstaller -Runtime dotnet -Version '9.0.22' -InstallDir $taskSdk -NoPath
}
& (Join-Path $taskSdk 'dotnet.exe') --info
