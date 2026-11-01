param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$siteRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/docs-site'))
$apiRoot = Join-Path $repositoryRoot 'docs/site/api'
$required = @('index.html', 'docs/introduction.html', 'docs/getting-started.html',
    'api/Calendar.WinUI.Controls.Calendar.html', 'api/Calendar.Core.Events.CalendarTimeKind.html',
    'api/toc.json', 'docs/toc.json', 'index.json', '.nojekyll')
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $siteRoot $relative) -PathType Leaf)) { throw "Missing site file: $relative" }
}
$navbar = Get-Content -LiteralPath (Join-Path $siteRoot 'toc.json') -Raw | ConvertFrom-Json
if (($navbar.items.name -join ',') -ne 'Docs,API Reference' -or @($navbar.items | Where-Object items).Count) {
    throw 'The header must have two flat navigation tabs.'
}
$apiToc = Get-Content -LiteralPath (Join-Path $siteRoot 'api/toc.json') -Raw | ConvertFrom-Json
if (-not $apiToc.items.Count -or @($apiToc.items | Where-Object {$_.type -ne 'Namespace'}).Count) {
    throw 'API navigation must be generated and grouped by namespace.'
}
$htmlFiles = @(Get-ChildItem -LiteralPath $siteRoot -Recurse -Filter '*.html')
$contentByPath = @{}
$idsByPath = @{}
foreach ($file in $htmlFiles) {
    $html = [IO.File]::ReadAllText($file.FullName)
    if (($html -match '<html\b' -and $html -notmatch '<html lang="en">') -or $html -match '[\uAC00-\uD7A3]') {
        throw "Documentation must be English-only: $($file.Name)"
    }
    $contentByPath[$file.FullName] = $html
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($match in [regex]::Matches($html, '\bid="([^"]+)"')) { $null = $ids.Add([Net.WebUtility]::HtmlDecode($match.Groups[1].Value)) }
    $idsByPath[$file.FullName] = $ids
}
$linkCount = 0
foreach ($file in $htmlFiles) {
    $relativePage = [IO.Path]::GetRelativePath($siteRoot, $file.FullName).Replace('\', '/')
    $pageBase = [Uri]::new("https://calendar.invalid/$relativePage")
    foreach ($match in [regex]::Matches($contentByPath[$file.FullName], '\b(?:href|src)="([^"]+)"')) {
        $link = [Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
        if (-not $link -or $link -match '^(?:[a-z][a-z0-9+.-]*:|//)') { continue }
        if ($link.StartsWith('/')) { throw "Root-absolute link breaks GitHub project Pages: $link" }
        $target = [Uri]::new($pageBase, $link)
        $path = Join-Path $siteRoot ([Uri]::UnescapeDataString($target.AbsolutePath).TrimStart('/'))
        if (Test-Path -LiteralPath $path -PathType Container) { $path = Join-Path $path 'index.html' }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Broken site link in $($file.Name): $link" }
        if ($target.Fragment -and $idsByPath.ContainsKey($path)) {
            $id = [Uri]::UnescapeDataString($target.Fragment.TrimStart('#'))
            if (-not $idsByPath[$path].Contains($id)) { throw "Missing anchor in $($file.Name): $link" }
        }
        $linkCount++
    }
}
$calendarHtml = $contentByPath[(Join-Path $siteRoot 'api/Calendar.WinUI.Controls.Calendar.html')]
if ($calendarHtml -notmatch 'title="View source" href="https://github.com/byungmeo/Calendar.WinUI/blob/[^\"]+/src/Calendar.WinUI/Controls/Calendar.cs[^\"]*#L\d+') {
    throw 'Calendar View source must link to its handwritten C# declaration.'
}
$enumHtml = $contentByPath[(Join-Path $siteRoot 'api/Calendar.Core.Events.CalendarTimeKind.html')]
if ($enumHtml -notmatch 'A wall-clock time or interval without a time zone\.') { throw 'Enum member XML summary was not rendered.' }
if ($calendarHtml -notmatch 'docfx:tocrel" content="toc.html"') { throw 'API sidebar must use the generated API TOC.' }
$trackedApi = @(& git -C $repositoryRoot ls-files docs/site/api)
if ($LASTEXITCODE -ne 0 -or $trackedApi.Count) { throw 'Generated API files must not be tracked in Git.' }
$report = [ordered]@{
    htmlPages=$htmlFiles.Count; checkedLocalLinks=$linkCount
    namespaceCount=$apiToc.items.Count
    generatedApiFiles=@(Get-ChildItem -LiteralPath $apiRoot -Filter '*.yml').Count
    viewSource=$true; xmlSummaries=$true; flatTabs=$true; projectPagesPaths=$true; englishOnly=$true
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repositoryRoot 'artifacts/docs-verification.json')
$report | ConvertTo-Json
