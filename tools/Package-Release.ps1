param(
    [string]$Version = '0.1.0'
)

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repoRoot 'src\CSVForge.App.Wpf\CSVForge.App.Wpf.csproj'
$publishRoot = Join-Path $repoRoot 'artifacts\publish'
$packageRoot = Join-Path $repoRoot 'artifacts\packages'
[System.IO.Directory]::CreateDirectory($packageRoot) | Out-Null

foreach ($profile in 'FrameworkDependent', 'SelfContained') {
    dotnet publish $project -p:PublishProfile=$profile | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Publishing profile $profile failed." }

    $folderName = if ($profile -eq 'FrameworkDependent') { 'framework-dependent' } else { 'self-contained' }
    $source = Join-Path $publishRoot $folderName
    $archive = Join-Path $packageRoot "CSVForge-$Version-$folderName-win-x64.zip"
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    Compress-Archive -Path (Join-Path $source '*') -DestinationPath $archive -CompressionLevel Optimal
    Write-Host "Created $archive"
}
