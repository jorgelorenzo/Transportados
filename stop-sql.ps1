# Stop SQL Server container
# Usage: .\stop-sql.ps1

Write-Host "Stopping SQL Server container..." -ForegroundColor Cyan

$scriptRoot = if ($PSScriptRoot) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}
$composeFile = Join-Path $scriptRoot "docker-compose.yml"
$composeArgs = @("-f", $composeFile, "--project-directory", $scriptRoot)

docker compose @composeArgs down

if ($LASTEXITCODE -eq 0) {
    Write-Host "SQL Server container stopped." -ForegroundColor Green
    Write-Host "Data volume preserved. Use .\start-sql-clean.ps1 to remove data." -ForegroundColor Yellow
}
else {
    Write-Host "Failed to stop container." -ForegroundColor Red
    exit 1
}
