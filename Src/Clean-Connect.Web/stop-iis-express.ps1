$ErrorActionPreference = 'SilentlyContinue'

$targets = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'iisexpress.exe' -or
    ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -like '*Clean-Connect.Web.dll*')
}

foreach ($t in $targets)
{
    Stop-Process -Id $t.ProcessId -Force -ErrorAction SilentlyContinue
}