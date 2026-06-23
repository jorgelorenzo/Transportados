# Start SQL Server container with fresh database (removes existing volume)
# Usage: .\start-sql-clean.ps1

Write-Host "Stopping and removing existing SQL Server container and data..." -ForegroundColor Yellow

$scriptRoot = if ($PSScriptRoot) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}
$composeFile = Join-Path $scriptRoot "docker-compose.yml"
$composeArgs = @("-f", $composeFile, "--project-directory", $scriptRoot)
$sqlContainerName = "transportados-sqlserver-dev"

docker compose @composeArgs down -v

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to stop and remove SQL Server container/data." -ForegroundColor Red
    exit 1
}

Write-Host "Starting fresh SQL Server container..." -ForegroundColor Cyan

docker compose @composeArgs up sqlserver -d

if ($LASTEXITCODE -eq 0) {
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow

    $maxAttempts = 30
    $attempt = 0

    while ($attempt -lt $maxAttempts) {
        $attempt++
        $health = docker inspect --format='{{.State.Health.Status}}' $sqlContainerName 2>$null

        if ($health -eq "healthy") {
            Write-Host "SQL Server is ready with fresh database!" -ForegroundColor Green
            Write-Host ""
            Write-Host "Connection details:" -ForegroundColor Cyan
            Write-Host "  Server: localhost,1436"
            Write-Host "  User: sa"
            Write-Host "  Password: YourStrong!Passw0rd"
            Write-Host "  Database: Transportados (created on first API run)"
            Write-Host ""
            Write-Host "You can now run .\start.ps1 to launch Transportados services." -ForegroundColor Green
            exit 0
        }

        Write-Host "  Attempt $attempt/$maxAttempts - Status: $health" -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }

    Write-Host "SQL Server did not become healthy in time. Check docker logs." -ForegroundColor Red
    exit 1
}
else {
    Write-Host "Failed to start SQL Server container." -ForegroundColor Red
    exit 1
}
