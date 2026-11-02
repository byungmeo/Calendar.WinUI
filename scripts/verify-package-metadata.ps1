param([string]$Version, [string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$metadataRoot = Split-Path $PSScriptRoot -Parent
if (-not $Version) {
    [xml]$properties = Get-Content (Join-Path $metadataRoot 'Directory.Build.props') -Raw
    $Version = $properties.SelectSingleNode('//Version').InnerText
}
if (-not $PackageDirectory) { $PackageDirectory = Join-Path $metadataRoot 'artifacts/packages' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$metadataResults = @()
foreach ($id in @('Byungmeo.Calendar.WinUI')) {
    $packageFile = Join-Path $PackageDirectory "$id.$Version.nupkg"
    $symbolFile = Join-Path $PackageDirectory "$id.$Version.snupkg"
    $archive = [IO.Compression.ZipFile]::OpenRead($packageFile)
    try {
        $manifestEntry = $archive.Entries | Where-Object FullName -like '*.nuspec' | Select-Object -First 1
        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $manifest.package.metadata
        foreach ($field in @('id','version','authors','description','projectUrl','readme','tags','releaseNotes','copyright')) {
            if ([string]::IsNullOrWhiteSpace([string]$metadata.$field)) { throw "$id is missing $field metadata." }
        }
        if ($metadata.id -ne $id -or $metadata.version -ne $Version) { throw "Unexpected package identity: $packageFile" }
        if ($metadata.license.type -ne 'expression' -or $metadata.license.InnerText -ne 'MIT') { throw "$id must declare its MIT license expression." }
        if ($metadata.repository.type -ne 'git' -or $metadata.repository.url -ne 'https://github.com/byungmeo/Calendar.WinUI' -or
            $metadata.repository.commit -notmatch '^[0-9a-f]{40}$') { throw "$id is missing its source repository or commit." }
        foreach ($entryName in @([string]$metadata.readme, 'LICENSE.txt')) {
            $entry = $archive.GetEntry($entryName)
            if (-not $entry) { throw "$id is missing $entryName." }
            $reader = [IO.StreamReader]::new($entry.Open())
            try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($text -match '\[year\]|\[fullname\]') { throw "$id contains template license placeholders." }
            if ($entryName -eq $metadata.readme) {
                if (-not $text.StartsWith('# Calendar.WinUI') -or -not $text.Contains("``$id``")) { throw "$id README must identify the product and its package ID." }
                foreach ($link in [regex]::Matches($text, '\[[^\]]+\]\(([^)]+)\)')) {
                    if ($link.Groups[1].Value -notmatch '^https://|^#') { throw "$id README has a link that will not resolve on NuGet.org." }
                }
            }
        }
        $dependencies = @($metadata.dependencies.group.dependency)
        if (@($dependencies | Where-Object id -eq 'Calendar.Core').Count) { throw 'Core must be bundled, not declared as a separate package dependency.' }
        if (@($dependencies | Where-Object id -eq 'Microsoft.Windows.SDK.BuildTools').Count) { throw 'Build-only tools must not be runtime package dependencies.' }
        $appSdkDependency = @($dependencies | Where-Object id -eq 'Microsoft.WindowsAppSDK')
        if ($appSdkDependency.Count -ne 1 -or $appSdkDependency[0].version -notin @('2.0.1', '[2.0.1,)')) {
            throw 'The public Windows App SDK dependency must allow 2.0.1 and later.'
        }
        $assemblyEntries = @($archive.Entries | Where-Object FullName -match '^lib/[^/]+/Calendar\.(Core|WinUI)\.dll$')
        if ($assemblyEntries.Count -ne 2) { throw 'The package must contain both Calendar assemblies in its lib assets.' }
        if (@($assemblyEntries | ForEach-Object { [IO.Path]::GetDirectoryName($_.FullName) } | Select-Object -Unique).Count -ne 1) {
            throw 'Both Calendar assemblies must use the same package target framework folder.'
        }
        foreach ($assembly in $assemblyEntries) {
            if (-not $archive.GetEntry([IO.Path]::ChangeExtension($assembly.FullName, '.xml'))) { throw "Missing XML documentation for $($assembly.FullName)." }
            $assemblyStream = $assembly.Open()
            $assemblyBuffer = [IO.MemoryStream]::new()
            try {
                $assemblyStream.CopyTo($assemblyBuffer)
                $assemblyBuffer.Position = 0
                $assemblyReader = [Reflection.PortableExecutable.PEReader]::new($assemblyBuffer)
                try {
                    $flags = $assemblyReader.PEHeaders.CorHeader.Flags
                    if ($assemblyReader.PEHeaders.CoffHeader.Machine -ne [Reflection.PortableExecutable.Machine]::I386 -or
                        -not $flags.HasFlag([Reflection.PortableExecutable.CorFlags]::ILOnly) -or
                        $flags.HasFlag([Reflection.PortableExecutable.CorFlags]::Requires32Bit) -or
                        $flags.HasFlag([Reflection.PortableExecutable.CorFlags]::Prefers32Bit)) {
                        throw "The bundled assembly must be AnyCPU: $($assembly.FullName)"
                    }
                } finally { $assemblyReader.Dispose() }
            } finally { $assemblyStream.Dispose(); $assemblyBuffer.Dispose() }
        }
    } finally { $archive.Dispose() }

    $symbols = [IO.Compression.ZipFile]::OpenRead($symbolFile)
    try {
        $invalidEntries = @($symbols.Entries | Where-Object { -not $_.FullName.EndsWith('/') -and $_.FullName -notmatch '\.(pdb|nuspec|xml|psmdcp|rels|p7s)$' })
        if ($invalidEntries.Count) { throw "$id has unsupported symbol-package entries: $($invalidEntries.FullName -join ', ')" }
        foreach ($assembly in $assemblyEntries) {
            $pdbPath = [IO.Path]::ChangeExtension($assembly.FullName, '.pdb')
            $pdb = $symbols.GetEntry($pdbPath)
            if (-not $pdb) { throw "Missing bundled symbols: $pdbPath" }
            $stream = $pdb.Open()
            $buffer = [IO.MemoryStream]::new()
            try { $stream.CopyTo($buffer); $bytes = $buffer.ToArray() } finally { $stream.Dispose(); $buffer.Dispose() }
            if ([Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'BSJB') { throw "$pdbPath must be a Portable PDB." }
            $pdbText = [Text.Encoding]::UTF8.GetString($bytes)
            $sourceUrl = 'https://raw.githubusercontent.com/byungmeo/Calendar.WinUI/' + $metadata.repository.commit + '/'
            if (-not $pdbText.Contains($sourceUrl)) { throw "$pdbPath is missing its commit-specific Source Link mapping." }
        }
    } finally { $symbols.Dispose() }
    $metadataResults += [pscustomobject]@{ id=$id; version=$Version; metadata='PASS'; readme='PASS'; license='MIT'; bundledAssemblies=@('Calendar.WinUI','Calendar.Core'); anyCpu=$true; separateCoreDependency=$false; windowsAppSdkMinimum='2.0.1'; portablePdb=$true; sourceLink=$true; package=$packageFile; symbols=$symbolFile }
}
$metadataResults | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $metadataRoot 'artifacts/package-metadata-verification.json')
$metadataResults | Format-Table id,version,metadata,readme,license,portablePdb,sourceLink
