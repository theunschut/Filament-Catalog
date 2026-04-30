#Requires -RunAsAdministrator

Stop-Service -Name "FilamentCatalog" -ErrorAction SilentlyContinue
# Remove-Service requires PS 6+; sc.exe works on PS 5.1 (Windows 11 default)
sc.exe delete FilamentCatalog | Out-Null
Write-Host "Service removed."
