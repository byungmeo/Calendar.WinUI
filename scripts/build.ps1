param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [ValidateSet('x64','x86','ARM64')][string]$Platform = 'x64',
    [ValidateSet('8.0','9.0','10.0')][string]$NetVersion = '10.0',
    [switch]$EnablePackageChecks,
    [switch]$PackagesOnly,
    [string]$PackageCache
)
$ErrorActionPreference = 'Stop'
$buildRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$buildArtifacts = [IO.Path]::GetFullPath((Join-Path $buildRoot 'artifacts'))
if (-not $PackageCache) { $PackageCache = Join-Path $buildArtifacts 'nuget/example' }
$PackageCache = [IO.Path]::GetFullPath($PackageCache)
if (-not $PackageCache.StartsWith($buildArtifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The example package cache must be inside this repository''s artifacts directory. The global NuGet cache is never modified.'
}
$buildExample = Join-Path $buildRoot 'samples/Calendar.Example/Calendar.Example.csproj'
Push-Location $buildRoot
try {
    # Retire artifacts with obsolete package IDs from the upload directory.
    $packageOutput = [IO.Path]::GetFullPath((Join-Path $buildArtifacts 'packages'))
    $legacyHistory = [IO.Path]::GetFullPath((Join-Path $buildArtifacts ('package-history/' + [guid]::NewGuid().ToString('N'))))
    foreach ($legacy in @(Get-ChildItem -LiteralPath $packageOutput -File -ErrorAction SilentlyContinue | Where-Object Name -match '^Calendar\.(Core|WinUI)\..*\.(nupkg|snupkg)$')) {
        $legacyPath = [IO.Path]::GetFullPath($legacy.FullName)
        $legacyDestination = [IO.Path]::GetFullPath((Join-Path $legacyHistory $legacy.Name))
        if (-not $legacyPath.StartsWith($packageOutput + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            -not $legacyDestination.StartsWith($buildArtifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Legacy package path escapes its workspace artifacts.'
        }
        New-Item -ItemType Directory -Path $legacyHistory -Force | Out-Null
        Move-Item -LiteralPath $legacyPath -Destination $legacyDestination
    }
    & dotnet pack src/Calendar.WinUI/Calendar.WinUI.csproj -c Release "-p:Platform=$Platform" -o artifacts/packages -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Byungmeo.Calendar.WinUI package build failed.' }
    & (Join-Path $PSScriptRoot 'verify-package-metadata.ps1')

    # Archive the current and legacy Calendar cache entries so repeated development versions restore fresh binaries.
    # Third-party entries remain cached. Keeping the old entries also avoids destructive cache cleanup.
    $buildHistory = [IO.Path]::GetFullPath((Join-Path $buildArtifacts ('nuget-history/' + [guid]::NewGuid().ToString('N'))))
    foreach ($id in @('byungmeo.calendar.winui','calendar.core','calendar.winui')) {
        $cached = [IO.Path]::GetFullPath((Join-Path $PackageCache $id))
        $archived = [IO.Path]::GetFullPath((Join-Path $buildHistory $id))
        if (-not $cached.StartsWith($PackageCache + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            -not $archived.StartsWith($buildArtifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Generated package path escapes its workspace cache.'
        }
        if (Test-Path -LiteralPath $cached) {
            New-Item -ItemType Directory -Path $buildHistory -Force | Out-Null
            Move-Item -LiteralPath $cached -Destination $archived
        }
    }
    if (-not $PackagesOnly) {
        $buildProperties = @("-p:Platform=$Platform", "-p:ExampleNetVersion=$NetVersion", "-p:RestorePackagesPath=$PackageCache",
            "-p:EnablePackageChecks=$($EnablePackageChecks.IsPresent.ToString().ToLowerInvariant())")
        & dotnet restore $buildExample @buildProperties --force --force-evaluate -v:minimal
        if ($LASTEXITCODE -ne 0) { throw 'Example package restore failed.' }
        & dotnet build $buildExample -c $Configuration @buildProperties --no-restore -v:minimal
        if ($LASTEXITCODE -ne 0) { throw 'Example build failed.' }
    }
    Write-Host "Local packages: $(Join-Path $buildArtifacts 'packages')"
    Write-Host "Example package cache: $PackageCache"
} finally { Pop-Location }
