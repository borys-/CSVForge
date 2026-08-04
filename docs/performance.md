# Performance baseline

Measured on 2026-07-22 with .NET 10 Release builds and SQLite-backed workspaces.
The datasets contain repeated keys so duplicate, comparison, and join paths produce non-trivial results.

| Rows | Import | Duplicates | Compare | Join |
| ---: | ---: | ---: | ---: | ---: |
| 10,000 | 0.559 s | 0.588 s | 3.801 s | 0.707 s |
| 50,000 | 0.972 s | 0.666 s | 1.095 s | 2.142 s |
| 100,000 | 1.481 s | 0.846 s | 1.550 s | 3.252 s |

Import values are medians of three runs using a prepared SQLite command and the default 5,000-row batch size. Other operation baselines are unchanged from the original measurement run.

Run `tools/Measure-Performance.ps1` to reproduce the measurements. Results and generated datasets are stored under the ignored `artifacts/` directory.

## Million-row import optimization

Measured on 2026-08-03 using `people-1000000.csv` (1,000,000 rows, four columns), Release builds, and fresh SQLite workspaces.

| Import batch | Median | Best |
| ---: | ---: | ---: |
| 5,000 rows | 4.942 s | 4.548 s |
| 100,000 rows | 4.157 s | 4.018 s |

Increasing the transaction batch reduced the median import time by approximately 15.9%. The optimized importer also trims values only when binding selected fields instead of allocating another array for every parsed row. Cancellation is checked every 1,024 rows, which remains responsive while avoiding one check per record.

Generate the dataset with:

```powershell
.\tools\Generate-PerformanceData.ps1 -RowCounts 1000000
```

The import still accepts `--batch-size` for controlled comparisons.
