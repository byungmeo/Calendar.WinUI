param([switch]$Serve, [ValidateRange(1,65535)][int]$Port = 8080)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (-not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
    throw 'The WinUI project metadata build requires Windows. The generated site is platform-independent static HTML.'
}
Push-Location $repositoryRoot
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'DocFX tool restore failed.' }
    & dotnet build src/Calendar.WinUI/Calendar.WinUI.csproj -c Release -p:Platform=x64 -p:TreatWarningsAsErrors=true -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Calendar library/XML documentation build failed.' }

    # Only generated documentation directories are removed; handwritten guides and comments remain.
    foreach ($relativePath in @('docs/site/api', 'docs/site/obj', 'artifacts/docs-site')) {
        $generatedPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $relativePath))
        if (-not $generatedPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Generated path escapes the repository: $generatedPath"
        }
        if (Test-Path -LiteralPath $generatedPath) { Remove-Item -LiteralPath $generatedPath -Recurse -Force }
    }
    & dotnet docfx docs/site/docfx.json --warningsAsErrors
    if ($LASTEXITCODE -ne 0) { throw 'DocFX metadata/site generation failed.' }
    & (Join-Path $PSScriptRoot 'verify-docs.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Documentation verification failed.' }
    Write-Host 'Static site: artifacts/docs-site'
    if ($Serve) {
        & dotnet docfx serve artifacts/docs-site --hostname 127.0.0.1 --port $Port
        if ($LASTEXITCODE -ne 0) { throw 'Documentation preview failed.' }
    }
} finally { Pop-Location }
