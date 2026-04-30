#Requires -RunAsAdministrator

Stop-Service -Name "FilamentCatalog" -ErrorAction SilentlyContinue
Remove-Service -Name "FilamentCatalog"
Write-Host "Service removed."
