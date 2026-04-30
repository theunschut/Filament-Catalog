#Requires -RunAsAdministrator
param(
    [string]$PublishDir = "$PSScriptRoot\publish"
)

$exePath = Join-Path $PublishDir "FilamentCatalog.exe"

if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found at '$exePath'. Run 'dotnet publish' first."
    exit 1
}

New-Service -Name "FilamentCatalog" `
            -BinaryPathName $exePath `
            -DisplayName "Filament Catalog" `
            -Description "Local filament spool tracker" `
            -StartupType Automatic

Start-Service -Name "FilamentCatalog"
Write-Host "Service installed and started. Browse to http://localhost:5000"
