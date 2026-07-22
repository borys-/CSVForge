# Performance baseline

Measured on 2026-07-22 with .NET 10 Release builds and SQLite-backed workspaces.
The datasets contain repeated keys so duplicate, comparison, and join paths produce non-trivial results.

| Rows | Import | Duplicates | Compare | Join |
| ---: | ---: | ---: | ---: | ---: |
| 10,000 | 1.265 s | 0.588 s | 3.801 s | 0.707 s |
| 50,000 | 2.892 s | 0.666 s | 1.095 s | 2.142 s |
| 100,000 | 3.964 s | 0.846 s | 1.550 s | 3.252 s |

Run `tools/Measure-Performance.ps1` to reproduce the measurements. Results and generated datasets are stored under the ignored `artifacts/` directory.
