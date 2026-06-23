# Start SQL Server container for local development
# Usage: .\start-sql.ps1

Write-Host "Starting SQL Server container..." -ForegroundColor Cyan

$scriptRoot = if ($PSScriptRoot) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}
$composeFile = Join-Path $scriptRoot "docker-compose.yml"
$composeArgs = @("-f", $composeFile, "--project-directory", $scriptRoot)
$sqlContainerName = "transportados-sqlserver-dev"

docker compose @composeArgs up sqlserver -d

if ($LASTEXITCODE -eq 0) {
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow

    $maxAttempts = 30
    $attempt = 0

    while ($attempt -lt $maxAttempts) {
        $attempt++
        $health = docker inspect --format='{{.State.Health.Status}}' $sqlContainerName 2>$null

        if ($health -eq "healthy") {
            Write-Host "SQL Server is ready!" -ForegroundColor Green
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
