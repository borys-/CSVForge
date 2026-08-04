param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\performance-data'),
    [int[]]$RowCounts = @(10000, 50000, 100000)
)

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

foreach ($rowCount in $RowCounts) {
    $path = Join-Path $resolvedOutput "people-$rowCount.csv"
    $writer = [System.IO.StreamWriter]::new($path, $false, [System.Text.UTF8Encoding]::new($true))
    try {
        $writer.WriteLine('Id;Email;Name;City')
        for ($index = 1; $index -le $rowCount; $index++) {
            $duplicateId = $index % [Math]::Max(1, [int]($rowCount / 10))
            $writer.WriteLine("$duplicateId;user$duplicateId@example.com;User $index;City $($index % 100)")
        }
    }
    finally {
        $writer.Dispose()
    }
    Write-Host "Generated $path ($rowCount rows)"
}
