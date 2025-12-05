# Create release ZIP for distribution
Param(
    [string]$Version = "1.0.0",
    [string]$OutputRoot = "artifacts"
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot
Set-Location $projectRoot

Write-Host "[INFO] Creating release package v$Version..." -ForegroundColor Cyan

# Build the package using package.ps1
& "$scriptRoot\package.ps1" -OutputRoot $OutputRoot -SelfContained

# Find the most recent package directory
$packageRoot = Join-Path $projectRoot $OutputRoot
$latestPackage = Get-ChildItem -Path $packageRoot -Directory -Filter "UniversalAnalogInput-*" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $latestPackage) {
    Write-Host "[ERROR] No package found in $packageRoot" -ForegroundColor Red
    exit 1
}

Write-Host "[INFO] Package trouvé : $($latestPackage.Name)" -ForegroundColor Cyan

# Create ZIP file
$zipName = "UniversalAnalogInput-v$Version-win-x64.zip"
$zipPath = Join-Path $packageRoot $zipName

Write-Host "[INFO] Création du ZIP : $zipName..." -ForegroundColor Cyan

# Remove existing ZIP if present
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Create ZIP using .NET (faster than Compress-Archive for large files)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $latestPackage.FullName,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false  # Don't include base directory in ZIP
)

$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "`n✓ Release créée avec succès !" -ForegroundColor Green
Write-Host "  Fichier : $zipPath" -ForegroundColor White
Write-Host "  Taille  : $([math]::Round($zipSize, 2)) MB" -ForegroundColor White
Write-Host "`nPrêt pour distribution sur GitHub Releases ou autres plateformes." -ForegroundColor Green
