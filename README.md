# CSVForge

CSVForge is a Windows desktop and CLI tool for importing, browsing, comparing, joining, deduplicating, and exporting CSV datasets through a SQLite-backed workspace.

The initial project scope and implementation roadmap are tracked in [plan.md](plan.md).

## Requirements

- Windows 10 or newer
- .NET 10 SDK for building from source

## Build and run

```powershell
dotnet build CSVForge.sln
dotnet run --project src/CSVForge.App.Wpf
dotnet run --project src/CSVForge.Cli -- --help
```

## CLI example

```powershell
dotnet run --project src/CSVForge.Cli -- workspace --action create --path data/workspace.db
dotnet run --project src/CSVForge.Cli -- import --workspace data/workspace.db --file samples/people.csv
dotnet run --project src/CSVForge.Cli -- list-tables --workspace data/workspace.db
```

Every data command accepts `--workspace`. Run the CLI with `--help` for examples of duplicate search, comparison, joining, and export.

## Publish

```powershell
dotnet publish src/CSVForge.App.Wpf -p:PublishProfile=FrameworkDependent
dotnet publish src/CSVForge.App.Wpf -p:PublishProfile=SelfContained
```

Artifacts are written under `artifacts/publish/`. The framework-dependent build requires the .NET 10 Desktop Runtime; the self-contained build includes its runtime.

Run `tools/Package-Release.ps1` to publish both variants and create versioned Windows ZIP packages plus an MSIX package under `artifacts/packages/`. Pass `-CertificateThumbprint` for a certificate whose subject is `CN=CSVForge` to sign the MSIX; without it, the package is intentionally left unsigned for CI signing.

## Performance checks

Run `tools/Measure-Performance.ps1` to generate deterministic datasets with 10,000, 50,000 and 100,000 rows and measure import, duplicate search, comparison, and join operations. Results are written to `artifacts/performance-results.csv`.
