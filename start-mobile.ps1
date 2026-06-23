param(
    [ValidateSet("Debug", "Release", "Local")]
    [string]$Configuration = "Debug",
    [switch]$SkipBuild,
    [switch]$CleanMobileBuild,
    [switch]$ResetAndroidApp,
    [switch]$SkipEmulatorStart,
    [int]$BackendStartupTimeoutSec = 0,
    [int]$AndroidStartupTimeoutSec = 180,
    [string]$AndroidPackageName = $env:TRANSPORTADOS_ANDROID_PACKAGE_NAME,
    [string]$AndroidAvdName = $env:TRANSPORTADOS_DEV_ANDROID_AVD_NAME,
    [string]$AndroidDevice = $env:TRANSPORTADOS_DEV_ANDROID_DEVICE,
    [string]$AndroidWifiDevice = $env:TRANSPORTADOS_DEV_ANDROID_WIFI_DEVICE,
    [ValidateSet("Physical", "Emulator", "Any")]
    [string]$AndroidTarget = $env:TRANSPORTADOS_DEV_ANDROID_TARGET,
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
$backendProject = Join-Path $repoRoot "src\apps\transportados\backend\Transportados.Api\Transportados.Api.csproj"
$mobileProject = Join-Path $repoRoot "src\apps\transportados\frontend\Transportados.Mobile\Transportados.Mobile.csproj"
$restoreConfigDirectory = Join-Path $repoRoot "logs\agent"
$restoreConfigPath = Join-Path $restoreConfigDirectory "start.nuget.config"
$sqlStartScript = Join-Path $repoRoot "start-sql.ps1"
$sqlContainerName = "transportados-sqlserver-dev"
$sqlHostPort = 1436
$backendLocalUrl = "http://localhost:7306"
$backendHealthUrl = "$backendLocalUrl/api/health"
$backendSwaggerLocalUrl = "$backendLocalUrl/swagger"
$mobileBackendUrl = "https://transportados-api-rj.desarrollo.net.ar/"
$defaultBackendConnectionString = "Server=localhost,$sqlHostPort;Database=Transportados;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;"

if ([string]::IsNullOrWhiteSpace($BackendConnectionString)) {
    $BackendConnectionString = $defaultBackendConnectionString
}

if ([string]::IsNullOrWhiteSpace($AndroidPackageName)) {
    $AndroidPackageName = "com.transportados"
}

if ($SkipBuild -and $CleanMobileBuild) {
    throw "Cannot use -SkipBuild and -CleanMobileBuild together."
}

if ([string]::IsNullOrWhiteSpace($AndroidAvdName)) {
    $AndroidAvdName = Get-DotEnvValue -Path $localEnvPath -Name "TRANSPORTADOS_DEV_ANDROID_AVD_NAME"
}

if ([string]::IsNullOrWhiteSpace($AndroidDevice)) {
    $AndroidDevice = Get-DotEnvValue -Path $localEnvPath -Name "TRANSPORTADOS_DEV_ANDROID_DEVICE"
}

if ([string]::IsNullOrWhiteSpace($AndroidWifiDevice)) {
    $AndroidWifiDevice = Get-DotEnvValue -Path $localEnvPath -Name "TRANSPORTADOS_DEV_ANDROID_WIFI_DEVICE"
}

if ([string]::IsNullOrWhiteSpace($AndroidTarget)) {
    $AndroidTarget = Get-DotEnvValue -Path $localEnvPath -Name "TRANSPORTADOS_DEV_ANDROID_TARGET"
}

if ([string]::IsNullOrWhiteSpace($AndroidTarget)) {
    $AndroidTarget = "Physical"
}

if (@("Physical", "Emulator", "Any") -notcontains $AndroidTarget) {
    throw "Invalid Android target '$AndroidTarget'. Use Physical, Emulator, or Any."
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

function Resolve-AndroidTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutableName,
        [string[]]$SdkRelativePaths = @()
    )

    $command = Get-Command $ExecutableName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $sdkRoots = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT)
    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $sdkRoots += Join-Path $env:LOCALAPPDATA "Android\Sdk"
    }
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $sdkRoots += Join-Path ${env:ProgramFiles(x86)} "Android\android-sdk"
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $sdkRoots += Join-Path $env:ProgramFiles "Android\android-sdk"
    }
    $sdkRoots += @("C:\Microsoft\AndroidSDK", "C:\Android\android-sdk")

    $sdkRoots = $sdkRoots |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique

    foreach ($sdkRoot in $sdkRoots) {
        foreach ($relativePath in $SdkRelativePaths) {
            $candidate = Join-Path $sdkRoot $relativePath
            if (Test-Path -LiteralPath $candidate) {
                return $candidate
            }
        }
    }

    return $null
}

function Get-AndroidDevices {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AdbPath
    )

    $rawDevices = & $AdbPath devices 2>$null
    $devices = @()
    foreach ($line in $rawDevices) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed -eq "List of devices attached") {
            continue
        }

        if ($trimmed -notmatch "^(?<serial>\S+)\s+(?<state>\S+)") {
            continue
        }

        $devices += [pscustomobject]@{
            Serial     = $matches["serial"]
            State      = $matches["state"]
            IsEmulator = $matches["serial"] -like "emulator-*"
        }
    }

    return $devices
}

function Test-IsAdbWifiEndpoint {
    param(
        [AllowNull()]
        [string]$Device
    )

    return -not [string]::IsNullOrWhiteSpace($Device) -and $Device -match "^[^:\s]+:\d+$"
}

function Connect-AndroidWifiDevice {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AdbPath,
        [Parameter(Mandatory = $true)]
        [string]$DeviceEndpoint
    )

    Write-Host "Connecting to Android device over WiFi at '$DeviceEndpoint'..." -ForegroundColor Cyan
    $connectOutput = (& $AdbPath connect $DeviceEndpoint 2>&1) -join "`n"
    if ($LASTEXITCODE -ne 0) {
        throw "adb connect failed for '$DeviceEndpoint'. Output: $connectOutput"
    }

    if ($connectOutput -match "connected to|already connected to") {
        return
    }

    throw "adb connect did not confirm a connection to '$DeviceEndpoint'. Output: $connectOutput"
}

function Get-AndroidAvds {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EmulatorPath
    )

    $avds = & $EmulatorPath -list-avds 2>$null |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    return @($avds)
}

function Start-AndroidEmulator {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EmulatorPath,
        [string]$AvdName
    )

    $selectedAvd = $AvdName
    if ([string]::IsNullOrWhiteSpace($selectedAvd)) {
        $availableAvds = Get-AndroidAvds -EmulatorPath $EmulatorPath
        if (-not $availableAvds) {
            throw "No Android AVDs were found. Create one in Android Device Manager or set TRANSPORTADOS_DEV_ANDROID_AVD_NAME."
        }

        $selectedAvd = $availableAvds[0]
    }

    Write-Host "Starting Android emulator '$selectedAvd'..." -ForegroundColor Cyan
    Start-Process -FilePath $EmulatorPath -ArgumentList @("-avd", $selectedAvd) | Out-Null
    return $selectedAvd
}

function Wait-ForAndroidDevice {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AdbPath,
        [string]$PreferredDevice,
        [int]$TimeoutSec = 180,
        [ValidateSet("Physical", "Emulator", "Any")]
        [string]$TargetKind = "Physical"
    )

    & $AdbPath start-server | Out-Null
    $startTime = Get-Date
    $attempt = 0

    while ($true) {
        $readyDevices = @(Get-AndroidDevices -AdbPath $AdbPath | Where-Object { $_.State -eq "device" })

        if (-not [string]::IsNullOrWhiteSpace($PreferredDevice)) {
            $matchedDevice = $readyDevices | Where-Object { $_.Serial -eq $PreferredDevice } | Select-Object -First 1
            if ($matchedDevice) {
                return $matchedDevice
            }
        }
        elseif ($TargetKind -eq "Physical") {
            $matchedDevice = $readyDevices | Where-Object { -not $_.IsEmulator } | Select-Object -First 1
            if ($matchedDevice) {
                return $matchedDevice
            }
        }
        elseif ($TargetKind -eq "Emulator") {
            $matchedDevice = $readyDevices | Where-Object { $_.IsEmulator } | Select-Object -First 1
            if ($matchedDevice) {
                return $matchedDevice
            }
        }
        elseif ($readyDevices) {
            $matchedDevice = $readyDevices | Where-Object { -not $_.IsEmulator } | Select-Object -First 1
            if ($matchedDevice) {
                return $matchedDevice
            }

            return ($readyDevices | Select-Object -First 1)
        }

        $attempt++
        $elapsed = [int]((Get-Date) - $startTime).TotalSeconds
        if ($TimeoutSec -gt 0 -and $elapsed -ge $TimeoutSec) {
            $targetDescription = if ([string]::IsNullOrWhiteSpace($PreferredDevice)) { "$TargetKind Android device" } else { "Android device '$PreferredDevice'" }
            throw "Timed out waiting for $targetDescription to appear in adb devices."
        }

        if ($attempt -eq 1 -or $attempt % 10 -eq 0) {
            Write-Host "Waiting for Android device... waited $elapsed seconds." -ForegroundColor Yellow
        }

        Start-Sleep -Seconds 2
    }
}

function Wait-ForAndroidBoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AdbPath,
        [Parameter(Mandatory = $true)]
        [string]$DeviceSerial,
        [int]$TimeoutSec = 180
    )

    $startTime = Get-Date
    $attempt = 0

    while ($true) {
        $bootCompleted = ((& $AdbPath -s $DeviceSerial shell getprop sys.boot_completed 2>$null) -join "").Trim()
        if ($bootCompleted -eq "1") {
            & $AdbPath -s $DeviceSerial shell input keyevent 82 2>$null | Out-Null
            return
        }

        $attempt++
        $elapsed = [int]((Get-Date) - $startTime).TotalSeconds
        if ($TimeoutSec -gt 0 -and $elapsed -ge $TimeoutSec) {
            throw "Timed out waiting for Android device '$DeviceSerial' to finish booting."
        }

        if ($attempt -eq 1 -or $attempt % 10 -eq 0) {
            Write-Host "Android device '$DeviceSerial' is still booting... waited $elapsed seconds." -ForegroundColor Yellow
        }

        Start-Sleep -Seconds 2
    }
}

Ensure-RestoreConfig
$restoreMsBuildArgs = @(
    "/p:RestoreConfigFile=$restoreConfigPath",
    "/p:RestoreRootConfigDirectory=$repoRoot"
)
$mobileMsBuildArgs = $restoreMsBuildArgs + @(
    "/p:TransportadosAndroidPackageName=$AndroidPackageName"
)

if (-not $SkipBuild) {
    Write-Host "Building Transportados backend..." -ForegroundColor Cyan
    $backendBuildArgs = @("build", $backendProject, "--configuration", $Configuration) + $restoreMsBuildArgs
    Invoke-Dotnet -DotnetArgs $backendBuildArgs

    if ($CleanMobileBuild) {
        Write-Host "Cleaning Transportados mobile Android build outputs..." -ForegroundColor Cyan
        $mobileCleanArgs = @("clean", $mobileProject, "--configuration", $Configuration, "--framework", "net10.0-android") + $mobileMsBuildArgs
        Invoke-Dotnet -DotnetArgs $mobileCleanArgs
    }

    Write-Host "Building Transportados mobile app..." -ForegroundColor Cyan
    $mobileBuildArgs = @("build", $mobileProject, "--configuration", $Configuration, "--framework", "net10.0-android") + $mobileMsBuildArgs
    Invoke-Dotnet -DotnetArgs $mobileBuildArgs
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

$adbPath = Resolve-AndroidTool -ExecutableName "adb.exe" -SdkRelativePaths @("platform-tools\adb.exe")
if (-not $adbPath) {
    throw "adb.exe was not found. Install Android platform-tools or set ANDROID_HOME / ANDROID_SDK_ROOT."
}

& $adbPath start-server | Out-Null
$androidWifiEndpoint = Select-FirstNonEmpty -Values @(
    $AndroidWifiDevice,
    $(if (Test-IsAdbWifiEndpoint -Device $AndroidDevice) { $AndroidDevice } else { "" })
)

if (-not [string]::IsNullOrWhiteSpace($androidWifiEndpoint)) {
    Connect-AndroidWifiDevice -AdbPath $adbPath -DeviceEndpoint $androidWifiEndpoint
    if ([string]::IsNullOrWhiteSpace($AndroidDevice)) {
        $AndroidDevice = $androidWifiEndpoint
    }
}

$readyEmulator = @(Get-AndroidDevices -AdbPath $adbPath | Where-Object { $_.State -eq "device" -and $_.IsEmulator } | Select-Object -First 1)
$shouldStartEmulator = $AndroidTarget -eq "Emulator" -and [string]::IsNullOrWhiteSpace($AndroidDevice) -and -not $readyEmulator -and -not $SkipEmulatorStart
if ($shouldStartEmulator) {
    $emulatorPath = Resolve-AndroidTool -ExecutableName "emulator.exe" -SdkRelativePaths @("emulator\emulator.exe")
    if (-not $emulatorPath) {
        throw "emulator.exe was not found. Install the Android emulator or start an emulator manually with -SkipEmulatorStart."
    }

    Start-AndroidEmulator -EmulatorPath $emulatorPath -AvdName $AndroidAvdName | Out-Null
}

$targetDevice = Wait-ForAndroidDevice -AdbPath $adbPath -PreferredDevice $AndroidDevice -TimeoutSec $AndroidStartupTimeoutSec -TargetKind $AndroidTarget
Wait-ForAndroidBoot -AdbPath $adbPath -DeviceSerial $targetDevice.Serial -TimeoutSec $AndroidStartupTimeoutSec

if ($ResetAndroidApp) {
    $installedPackagePath = ((& $adbPath -s $targetDevice.Serial shell pm path $AndroidPackageName 2>$null) -join "").Trim()
    if (-not [string]::IsNullOrWhiteSpace($installedPackagePath)) {
        Write-Host "Uninstalling Android app '$AndroidPackageName' from '$($targetDevice.Serial)' to clear launcher/splash cache..." -ForegroundColor Cyan
        $uninstallOutput = (& $adbPath -s $targetDevice.Serial uninstall $AndroidPackageName 2>&1) -join "`n"
        if ($LASTEXITCODE -ne 0) {
            throw "adb uninstall failed for '$AndroidPackageName'. Output: $uninstallOutput"
        }
    }
    else {
        Write-Host "Android app '$AndroidPackageName' is not installed on '$($targetDevice.Serial)'." -ForegroundColor DarkYellow
    }
}

Write-Host "Starting mobile app on Android device '$($targetDevice.Serial)'..." -ForegroundColor Cyan
$mobileNoBuild = if ($SkipBuild) { " --no-build" } else { "" }
$mobileRun = "dotnet run --project `"$mobileProject`" --configuration $Configuration --framework net10.0-android$mobileNoBuild /p:RestoreConfigFile=`"$restoreConfigPath`" /p:RestoreRootConfigDirectory=`"$repoRoot`" /p:TransportadosAndroidPackageName=`"$AndroidPackageName`""

$previousAndroidSerial = [Environment]::GetEnvironmentVariable("ANDROID_SERIAL", "Process")
[Environment]::SetEnvironmentVariable("ANDROID_SERIAL", $targetDevice.Serial, "Process")
try {
    Start-Process -FilePath "powershell" -WorkingDirectory $repoRoot -ArgumentList @("-NoExit", "-Command", $mobileRun) | Out-Null
}
finally {
    [Environment]::SetEnvironmentVariable("ANDROID_SERIAL", $previousAndroidSerial, "Process")
}

Write-Host ""
Write-Host "Started mobile development services:" -ForegroundColor Green
Write-Host "  Backend (local):          $backendHealthUrl"
Write-Host "  Backend Swagger (local):  $backendSwaggerLocalUrl"
Write-Host "  Mobile API URL:           $mobileBackendUrl"
Write-Host "  Android target:           $($targetDevice.Serial)"
Write-Host ""
Write-Host "Use Ctrl+C in each spawned PowerShell window to stop dotnet processes."
