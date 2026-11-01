# Development

## Repository layout

```text
src/
  Calendar.Core/          Framework-independent models and layout
  Calendar.WinUI/         WinUI control and resources
  Calendar.Core.Tests/    Core tests for .NET 8, 9, and 10
samples/
  Calendar.Example/       NuGet-consuming example and opt-in package checks
docs/
  site/                  DocFX guides, generated API reference, and site template
  *.md                   Design, maintenance, and validation notes
scripts/                 Build and verification entry points
artifacts/               Ignored packages, caches, build output, and reports
```

## Build and run

Use Windows and the SDK pinned in `global.json`:

```powershell
./scripts/build.ps1
winapp run samples/Calendar.Example/Calendar.Example.csproj --arch x64 --no-build --no-restore --detach --json
```

The build script creates one `Byungmeo.Calendar.WinUI` package in `artifacts/packages`, then restores and builds `Calendar.Example`. The package contains both `Calendar.WinUI.dll` and `Calendar.Core.dll` with their XML documentation. Core remains a separate, non-packable source project. The example has no `ProjectReference`; it consumes the single package with a `PackageReference`. Its explicit Windows App SDK references belong to the application itself.

The example uses the repository-owned `artifacts/nuget/example` package cache. Before restoring a newly packed development version, the script archives the Calendar package entry and any legacy standalone Core entry under `artifacts/nuget-history`. Third-party packages stay cached, and the global NuGet cache is never cleared. This prevents stale binaries when the same prerelease version is packed repeatedly. A custom `-PackageCache` must also be under this repository's `artifacts` directory.

After the first package build, the solution can also be built with `dotnet build Calendar.WinUI.slnx` or Visual Studio. Re-run the script after library changes to update the package consumed by the example. To prepare packages only, use `./scripts/build.ps1 -PackagesOnly`.

Optional build parameters include `-Configuration Release`, `-Platform ARM64`, and `-NetVersion 8.0`. The example defaults to .NET 10. `CalendarPackageVersion` can be overridden as an MSBuild property when consuming a different package version.

## NuGet packages

```powershell
./scripts/build.ps1 -PackagesOnly
```

This creates `Byungmeo.Calendar.WinUI.<version>.nupkg` and `Byungmeo.Calendar.WinUI.<version>.snupkg` under `artifacts/packages`. Publish only this package ID. The symbol package contains both assemblies' Portable PDBs with Source Link. Artifacts with the legacy `Calendar.Core` and `Calendar.WinUI` package IDs are moved out of the upload directory into `artifacts/package-history`.

The build automatically verifies package metadata, the README and MIT license, both bundled DLLs' AnyCPU headers and XML files, absence of a standalone Core dependency, both Portable PDBs, and commit-specific Source Link mappings. Run `scripts/verify-package-metadata.ps1` to repeat these checks without rebuilding.

Version and shared metadata are in `Directory.Build.props`; the package ID, description, tags, and release notes are in `src/Calendar.WinUI/Calendar.WinUI.csproj`. The package ID is independent of the unchanged assembly and namespace names. Before a public release, commit and push the release sources, then rebuild the packages so Source Link identifies the published source commit. Use a new version for each subsequent publication. Publishing is a separate action; the build scripts never push packages to NuGet.org.

## Verification

```powershell
dotnet test src/Calendar.Core.Tests/Calendar.Core.Tests.csproj
./scripts/verify-package.ps1
```

`verify-package.ps1` creates the local packages, builds the same example on .NET 8, 9, and 10, launches each actual application, and checks runtime versions, API regression groups, and date/event/overflow interactions. It verifies that the example has no project references. There is no separate consumer project.

Package checks live in `samples/Calendar.Example/MainWindow.PackageChecks.cs` and are compiled only with `EnablePackageChecks=true`. They add runtime/check status text, run the API scenarios, then restore the normal demonstration data. They are excluded from normal example builds. To inspect this mode manually:

```powershell
./scripts/build.ps1 -EnablePackageChecks
winapp run samples/Calendar.Example/Calendar.Example.csproj --arch x64 -p EnablePackageChecks=true --no-build --no-restore --detach --json
```

For the ordinary example, run `./scripts/build.ps1` again. For additional UI checks, pass the PID returned by `winapp run` to `scripts/ui-tests.ps1`, `scripts/verify-resize.ps1`, or `scripts/verify-locale-settings.ps1`. Close a running example before rebuilding or redeploying it.

## Editing documentation

Edit feature guides in `docs/site/docs/` and API descriptions in the libraries' C# XML comments. API pages and namespace navigation are generated by DocFX; do not edit generated files in `docs/site/api/`.

```powershell
./scripts/build-docs.ps1
./scripts/build-docs.ps1 -Serve
```

The script uses the pinned DocFX tool, builds the English-only site, and checks local links, API summaries, and source links. Output is in `artifacts/docs-site`; the preview runs at `http://127.0.0.1:8080`.

Site configuration and appearance are in `docs/site/docfx.json` and `docs/site/template/`. The GitHub Actions workflow builds documentation for pull requests targeting `master` and deploys pushes to `master` to GitHub Pages. No manual deployment is required.
