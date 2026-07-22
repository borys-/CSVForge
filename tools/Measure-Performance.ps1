param(
    [string]$DataDirectory = (Join-Path $PSScriptRoot '..\artifacts\performance-data'),
    [string]$ResultPath = (Join-Path $PSScriptRoot '..\artifacts\performance-results.csv')
)

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cli = Join-Path $repoRoot 'src\CSVForge.Cli\bin\Release\net10.0\CSVForge.Cli.dll'
dotnet build (Join-Path $repoRoot 'CSVForge.sln') --configuration Release | Out-Host
& (Join-Path $PSScriptRoot 'Generate-PerformanceData.ps1') -OutputDirectory $DataDirectory

$results = foreach ($rowCount in 10000, 50000, 100000) {
    $workspace = Join-Path $DataDirectory "benchmark-$rowCount.db"
    if (Test-Path -LiteralPath $workspace) { Remove-Item -LiteralPath $workspace -Force }
    dotnet $cli workspace --action create --path $workspace | Out-Null
    $csv = Join-Path $DataDirectory "people-$rowCount.csv"
    $elapsed = Measure-Command { dotnet $cli import --workspace $workspace --file $csv --name "People$rowCount" | Out-Null }
    $table = (dotnet $cli list-tables --workspace $workspace | Select-Object -First 1).Split("`t")[0]
    $duplicates = Measure-Command { dotnet $cli duplicates --workspace $workspace --table $table --columns Email | Out-Null }
    $compare = Measure-Command { dotnet $cli compare --workspace $workspace --left $table --right $table --left-keys Id --right-keys Id | Out-Null }
    $join = Measure-Command { dotnet $cli join --workspace $workspace --left $table --right $table --left-keys Id --right-keys Id | Out-Null }
    [pscustomobject]@{
        Rows = $rowCount
        ImportSeconds = [Math]::Round($elapsed.TotalSeconds, 3)
        DuplicatesSeconds = [Math]::Round($duplicates.TotalSeconds, 3)
        CompareSeconds = [Math]::Round($compare.TotalSeconds, 3)
        JoinSeconds = [Math]::Round($join.TotalSeconds, 3)
    }
}

[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($ResultPath))) | Out-Null
$results | Export-Csv -LiteralPath $ResultPath -NoTypeInformation -Encoding utf8
$results | Format-Table | Out-Host
