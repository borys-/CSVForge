param(
    [string]$Version = '0.1.0',
    [string]$CertificateThumbprint
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

$makeAppx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter 'makeappx.exe' -Recurse |
    Where-Object { $_.FullName -match '\\x64\\makeappx\.exe$' } | Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($makeAppx)) { throw 'Windows SDK makeappx.exe was not found.' }
$signTool = Join-Path (Split-Path $makeAppx) 'signtool.exe'
$layout = Join-Path $repoRoot 'artifacts\msix-layout'
if (Test-Path -LiteralPath $layout) { Remove-Item -LiteralPath $layout -Recurse -Force }
[System.IO.Directory]::CreateDirectory($layout) | Out-Null
Copy-Item -Path (Join-Path $publishRoot 'self-contained\*') -Destination $layout -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\AppxManifest.xml') -Destination $layout
Copy-Item -LiteralPath (Join-Path $repoRoot 'assets\app') -Destination (Join-Path $layout 'Assets') -Recurse
$msix = Join-Path $packageRoot "CSVForge-$Version-win-x64.msix"
if (Test-Path -LiteralPath $msix) { Remove-Item -LiteralPath $msix -Force }
& $makeAppx pack /d $layout /p $msix /o | Out-Host
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $msix)) { throw 'Building MSIX package failed.' }
if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 $msix | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Signing MSIX package failed.' }
}
Write-Host "Created $msix"
