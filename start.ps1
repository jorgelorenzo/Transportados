param(
    [ValidateSet("Debug", "Release", "Local")]
    [string]$Configuration = "Debug",
    [switch]$SkipBuild,
    [int]$BackendStartupTimeoutSec = 0,
    [string]$BackendPublicUrl = $env:TRANSPORTADOS_DEV_BACKEND_PUBLIC_URL,
    [string]$FrontendPublicUrl = $env:TRANSPORTADOS_DEV_FRONTEND_PUBLIC_URL,
    [string]$BackendConnectionString = $env:TRANSPORTADOS_DEV_BACKEND_CONNECTION_STRING,
    [string]$EmailTransportSenderName = $env:TRANSPORTADOS_DEV_EMAIL_TRANSPORT_SENDER_NAME,
    [string]$EmailTransportEmailFrom = $env:TRANSPORTADOS_DEV_EMAIL_TRANSPORT_EMAIL_FROM,
    [string]$EmailTransportHost = $env:TRANSPORTADOS_DEV_EMAIL_TRANSPORT_HOST,
    [string]$EmailTransportPort = $env:TRANSPORTADOS_DEV_EMAIL_TRANSPORT_PORT,
    [string]$EmailTransportUser = $env:TRANSPORTADOS_DEV_EMAIL_TRANSPORT_USER,
    [string]$EmailTransportPass = $env:TRANSPORTADOS_DEV_EMAIL_TRANSPORT_PASS,
    [string]$EmailTransportUseSsl = $env:TRANSPORTADOS_DEV_EMAIL_TRANSPORT_USE_SSL,
    [string]$EmailTransportCcEmails = $env:TRANSPORTADOS_DEV_EMAIL_TRANSPORT_CC_EMAILS
)

$ErrorActionPreference = "Stop"

function Normalize-WindowsPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ($Path.StartsWith("\\?\")) {
        return $Path.Substring(4)
    }

    return $Path
}

function Get-DotEnvValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf("=")
        if ($separatorIndex -le 0) {
            continue
        }

        $key = $trimmed.Substring(0, $separatorIndex).Trim()
        if ($key -ne $Name) {
            continue
        }

        return $trimmed.Substring($separatorIndex + 1).Trim()
    }

    return $null
}

function Select-FirstNonEmpty {
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string[]]$Values = @()
    )

    foreach ($value in $Values) {
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    return ""
}

$repoRootCandidate = if ($PSScriptRoot) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}

$repoRoot = Normalize-WindowsPath $repoRootCandidate
$localEnvPath = Join-Path $repoRoot "start.local.env"
$solution = Join-Path $repoRoot "Transportados.App.sln"
$backendProject = Join-Path $repoRoot "src\apps\transportados\backend\Transportados.Api\Transportados.Api.csproj"
$frontendProject = Join-Path $repoRoot "src\apps\transportados\frontend\Transportados.Web\Transportados.Web.csproj"
$restoreConfigDirectory = Join-Path $repoRoot "logs\agent"
$restoreConfigPath = Join-Path $restoreConfigDirectory "start.nuget.config"
$sqlStartScript = Join-Path $repoRoot "start-sql.ps1"
$sqlContainerName = "transportados-sqlserver-dev"
$sqlHostPort = 1436
$backendLocalUrl = "http://localhost:7306"
$backendHealthUrl = "$backendLocalUrl/api/health"
$backendSwaggerLocalUrl = "$backendLocalUrl/swagger"
$frontendLocalUrl = "http://localhost:5142"
$defaultBackendConnectionString = "Server=localhost,$sqlHostPort;Database=Transportados;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;"

if ([string]::IsNullOrWhiteSpace($BackendPublicUrl)) {
$BackendPublicUrl = "https://transportados-api-jl.desarrollo.net.ar/api/health"
}

if ([string]::IsNullOrWhiteSpace($FrontendPublicUrl)) {
$FrontendPublicUrl = "https://transportados-app-jl.desarrollo.net.ar"
}

if ([string]::IsNullOrWhiteSpace($BackendConnectionString)) {
    $BackendConnectionString = $defaultBackendConnectionString
}

$EmailTransportSenderName = Select-FirstNonEmpty -Values @(
    $EmailTransportSenderName,
    $env:Email__Transport__SenderName,
    (Get-DotEnvValue -Path $localEnvPath -Name "Email__Transport__SenderName"),
    "Transportados"
)
$EmailTransportEmailFrom = Select-FirstNonEmpty -Values @(
    $EmailTransportEmailFrom,
    $env:Email__Transport__EmailFrom,
    (Get-DotEnvValue -Path $localEnvPath -Name "Email__Transport__EmailFrom"),
    (Get-DotEnvValue -Path $localEnvPath -Name "Smtp__EmailFrom")
)
$EmailTransportHost = Select-FirstNonEmpty -Values @(
    $EmailTransportHost,
    $env:Email__Transport__Host,
    (Get-DotEnvValue -Path $localEnvPath -Name "Email__Transport__Host"),
    (Get-DotEnvValue -Path $localEnvPath -Name "Smtp__Host")
)
$EmailTransportPort = Select-FirstNonEmpty -Values @(
    $EmailTransportPort,
    $env:Email__Transport__Port,
    (Get-DotEnvValue -Path $localEnvPath -Name "Email__Transport__Port"),
    (Get-DotEnvValue -Path $localEnvPath -Name "Smtp__Port")
)
$EmailTransportUser = Select-FirstNonEmpty -Values @(
    $EmailTransportUser,
    $env:Email__Transport__User,
    (Get-DotEnvValue -Path $localEnvPath -Name "Email__Transport__User"),
    (Get-DotEnvValue -Path $localEnvPath -Name "Smtp__User")
)
$EmailTransportPass = Select-FirstNonEmpty -Values @(
    $EmailTransportPass,
    $env:Email__Transport__Pass,
    (Get-DotEnvValue -Path $localEnvPath -Name "Email__Transport__Pass"),
    (Get-DotEnvValue -Path $localEnvPath -Name "Smtp__Pass")
)
$EmailTransportUseSsl = Select-FirstNonEmpty -Values @(
    $EmailTransportUseSsl,
    $env:Email__Transport__UseSsl,
    (Get-DotEnvValue -Path $localEnvPath -Name "Email__Transport__UseSsl"),
    (Get-DotEnvValue -Path $localEnvPath -Name "Smtp__UseSSL"),
    "false"
)
$EmailTransportCcEmails = Select-FirstNonEmpty -Values @(
    $EmailTransportCcEmails,
    $env:Email__Transport__CcEmails,
    (Get-DotEnvValue -Path $localEnvPath -Name "Email__Transport__CcEmails")
)

$backendSwaggerPublicUrl = if ($BackendPublicUrl -match "/api/health/?$") {
    $BackendPublicUrl -replace "/api/health/?$", "/swagger"
}
else {
    "$($BackendPublicUrl.TrimEnd('/'))/swagger"
}

function Get-ProcessCommandLine {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    try {
        $proc = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction Stop
        return $proc.CommandLine
    }
    catch {
        return $null
    }
}

function Get-ListeningProcessesForPort {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $connections) {
        return @()
    }

    $processIds = $connections |
        Select-Object -ExpandProperty OwningProcess -Unique

    $processes = @()
    foreach ($processId in $processIds) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if (-not $process) {
            continue
        }

        $path = $null
        try {
            $path = $process.Path
        }
        catch {
        }

        $processes += [pscustomobject]@{
            Id          = $process.Id
            ProcessName = $process.ProcessName
            Path        = $path
            CommandLine = Get-ProcessCommandLine -ProcessId $process.Id
        }
    }

    return $processes
}

function Wait-ForPortToBeReleased {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port,
        [int]$TimeoutSec = 15
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-ListeningProcessesForPort -Port $Port)) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    return -not (Get-ListeningProcessesForPort -Port $Port)
}

function Test-IsExpectedTransportadosProcess {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$ProcessInfo,
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [string[]]$CommandMarkers = @()
    )

    $path = $ProcessInfo.Path
    $commandLine = $ProcessInfo.CommandLine

    if ($path -and $path -like "*$RepoRoot*") {
        return $true
    }

    if ($commandLine -and $commandLine -like "*$RepoRoot*") {
        return $true
    }

    foreach ($marker in $CommandMarkers) {
        if ($commandLine -and $commandLine -like "*$marker*") {
            return $true
        }
    }

    return $false
}

function Ensure-PortAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port,
        [Parameter(Mandatory = $true)]
        [string]$FriendlyName,
        [Parameter(Mandatory = $true)]
        [string[]]$ExpectedProcessNames,
        [string[]]$CommandMarkers = @()
    )

    $listeners = Get-ListeningProcessesForPort -Port $Port
    if (-not $listeners) {
        return
    }

    foreach ($listener in $listeners) {
        if (-not ($ExpectedProcessNames -contains $listener.ProcessName)) {
            $pathSuffix = if ($listener.Path) { " at $($listener.Path)" } else { "" }
            throw "$FriendlyName port $Port is in use by '$($listener.ProcessName)' (PID $($listener.Id))$pathSuffix. Stop that process manually before retrying."
        }

        if (-not (Test-IsExpectedTransportadosProcess -ProcessInfo $listener -RepoRoot $repoRoot -CommandMarkers $CommandMarkers)) {
            $cmdSuffix = if ($listener.CommandLine) { " CommandLine: $($listener.CommandLine)" } else { "" }
            throw "$FriendlyName port $Port is in use by '$($listener.ProcessName)' (PID $($listener.Id)) that does not look like an Transportados process.$cmdSuffix"
        }

        Write-Host "$FriendlyName port $Port is in use by stale Transportados process $($listener.ProcessName) (PID $($listener.Id)). Stopping it..." -ForegroundColor Yellow
        Stop-Process -Id $listener.Id -Force -ErrorAction Stop
    }

    if (-not (Wait-ForPortToBeReleased -Port $Port)) {
        throw "$FriendlyName port $Port did not become available after stopping stale Transportados processes."
    }
}

function Wait-Url {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [int]$TimeoutSec = 0,
        [System.Diagnostics.Process]$ProcessToWatch
    )

    $startTime = Get-Date
    $attempt = 0

    while ($true) {
        if ($ProcessToWatch -and $ProcessToWatch.HasExited) {
            return $false
        }

        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return $true
            }
        }
        catch {
        }

        $attempt++
        $elapsed = [int]((Get-Date) - $startTime).TotalSeconds

        if ($TimeoutSec -gt 0 -and $elapsed -ge $TimeoutSec) {
            return $false
        }

        if ($attempt -eq 1 -or $attempt % 10 -eq 0) {
            if ($TimeoutSec -gt 0) {
                Write-Host "Backend is still starting... waited $elapsed seconds so far (timeout: $TimeoutSec seconds)." -ForegroundColor Yellow
            }
            else {
                Write-Host "Backend is still starting... waited $elapsed seconds so far." -ForegroundColor Yellow
            }
        }

        Start-Sleep -Seconds 2
    }
}

function Get-DockerContainerStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContainerName
    )

    $previousErrorActionPreference = $ErrorActionPreference

    try {
        $ErrorActionPreference = "Continue"
        $inspectOutput = & docker inspect --format='{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $ContainerName 2>&1
        $inspectExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($inspectExitCode -eq 0) {
        return ($inspectOutput | Select-Object -First 1)
    }

    $inspectMessage = ($inspectOutput | Out-String).Trim()
    if ($inspectMessage -match "no such object") {
        return ""
    }

    if ([string]::IsNullOrWhiteSpace($inspectMessage)) {
        $inspectMessage = "exit code $inspectExitCode"
    }

    throw "Failed to inspect Docker container '$ContainerName': $inspectMessage"
}

function Invoke-Dotnet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$DotnetArgs
    )

    & dotnet @DotnetArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: dotnet $($DotnetArgs -join ' ')"
    }
}

function Ensure-RestoreConfig {
    if (-not (Test-Path $restoreConfigDirectory)) {
        New-Item -ItemType Directory -Path $restoreConfigDirectory -Force | Out-Null
    }

    $configContent = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
'@

    Set-Content -Path $restoreConfigPath -Value $configContent -Encoding utf8
}

Ensure-RestoreConfig
$restoreMsBuildArgs = @(
    "/p:RestoreConfigFile=$restoreConfigPath",
    "/p:RestoreRootConfigDirectory=$repoRoot"
)

if (-not $SkipBuild) {
    Write-Host "Building Transportados solution..." -ForegroundColor Cyan
    $buildArgs = @("build", $solution, "--configuration", $Configuration) + $restoreMsBuildArgs
    Invoke-Dotnet -DotnetArgs $buildArgs
}

Write-Host "Checking local Transportados SQL Server container '$sqlContainerName' on localhost:$sqlHostPort..." -ForegroundColor Cyan
$sqlHealth = Get-DockerContainerStatus -ContainerName $sqlContainerName
$sqlPortIsUp = (Test-NetConnection -ComputerName "localhost" -Port $sqlHostPort -WarningAction SilentlyContinue).TcpTestSucceeded
$sqlIsUp = $sqlHealth -eq "healthy" -and $sqlPortIsUp
if (-not $sqlIsUp) {
    if (-not (Test-Path $sqlStartScript)) {
        throw "Transportados SQL Server container '$sqlContainerName' is not healthy/reachable on localhost:$sqlHostPort and start-sql.ps1 was not found."
    }

    Write-Host "Transportados SQL Server is not ready. Starting local Transportados SQL container..." -ForegroundColor Yellow
    & powershell -ExecutionPolicy Bypass -File $sqlStartScript
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start SQL Server. Transportados backend cannot start without database access."
    }
}

Ensure-PortAvailable -Port 7306 -FriendlyName "Backend" -ExpectedProcessNames @("Transportados.Api", "dotnet") -CommandMarkers @("Transportados.Api", "src\\apps\\transportados\\backend\\Transportados.Api")
Ensure-PortAvailable -Port 5142 -FriendlyName "Frontend HTTP" -ExpectedProcessNames @("Transportados.Web", "dotnet") -CommandMarkers @("Transportados.Web", "src\\apps\\transportados\\frontend\\Transportados.Web")

Write-Host "Starting backend..." -ForegroundColor Cyan
$backendEnvironmentOverrides = @{
    "ConnectionStrings__DefaultConnection" = $BackendConnectionString
    "UseInMemoryDatabase" = "false"
    "Email__Transport__SenderName" = $EmailTransportSenderName
    "Email__Transport__EmailFrom" = $EmailTransportEmailFrom
    "Email__Transport__Host" = $EmailTransportHost
    "Email__Transport__Port" = $EmailTransportPort
    "Email__Transport__User" = $EmailTransportUser
    "Email__Transport__Pass" = $EmailTransportPass
    "Email__Transport__UseSsl" = $EmailTransportUseSsl
    "Email__Transport__CcEmails" = $EmailTransportCcEmails
}

$previousBackendEnvironment = @{}
foreach ($key in $backendEnvironmentOverrides.Keys) {
    $previousBackendEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, "Process")
    [Environment]::SetEnvironmentVariable($key, [string]$backendEnvironmentOverrides[$key], "Process")
}

$backendRun = "dotnet run --project `"$backendProject`" --configuration $Configuration --urls http://localhost:7306 /p:RestoreConfigFile=`"$restoreConfigPath`" /p:RestoreRootConfigDirectory=`"$repoRoot`""
$backendProcess = Start-Process -FilePath "powershell" -WorkingDirectory $repoRoot -ArgumentList @("-NoExit", "-Command", $backendRun) -PassThru

foreach ($key in $previousBackendEnvironment.Keys) {
    [Environment]::SetEnvironmentVariable($key, $previousBackendEnvironment[$key], "Process")
}

Write-Host "Waiting for backend to be reachable..." -ForegroundColor Cyan
$backendReady = Wait-Url -Url $backendHealthUrl -TimeoutSec $BackendStartupTimeoutSec -ProcessToWatch $backendProcess
if (-not $backendReady) {
    if ($backendProcess.HasExited) {
        throw "Backend process exited before becoming reachable at $backendHealthUrl."
    }

    throw "Backend did not become reachable at $backendHealthUrl."
}

Write-Host "Starting frontend..." -ForegroundColor Cyan
$frontendRun = "dotnet run --project `"$frontendProject`" --configuration $Configuration --launch-profile http /p:RestoreConfigFile=`"$restoreConfigPath`" /p:RestoreRootConfigDirectory=`"$repoRoot`""
Start-Process -FilePath "powershell" -WorkingDirectory $repoRoot -ArgumentList @("-NoExit", "-Command", $frontendRun) | Out-Null

Write-Host ""
Write-Host "Started services:" -ForegroundColor Green
Write-Host "  Backend (public):  $BackendPublicUrl"
Write-Host "  Backend Swagger (public): $backendSwaggerPublicUrl"
Write-Host "  Frontend (public): $FrontendPublicUrl"
Write-Host "  Backend (local):   $backendHealthUrl"
Write-Host "  Backend Swagger (local):  $backendSwaggerLocalUrl"
Write-Host "  Frontend (local):  $frontendLocalUrl"
Write-Host ""
Write-Host "Use Ctrl+C in each spawned window to stop services."
