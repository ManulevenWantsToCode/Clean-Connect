param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = 'SilentlyContinue'

Write-Host "Stopping Clean-Connect IIS Express..."
Get-CimInstance Win32_Process | Where-Object {
    ($_.Name -eq 'iisexpress.exe' -and $_.CommandLine -like '*Clean-Connect*') -or
    ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*Clean-Connect.Web.dll*')
} | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Get-Process -Name iisexpresstray -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "Building..."
dotnet build (Join-Path $PSScriptRoot "Src\Clean-Connect.Web\Clean-Connect.Web.csproj") -c $Configuration --nologo

exit $LASTEXITCODE