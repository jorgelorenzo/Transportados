[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Environment = "staging",

    [Parameter(Mandatory = $false)]
    [ValidateNotNullOrEmpty()]
    [string[]]$Components,

    [Parameter(Mandatory = $true)]
    [string]$SshKeyPath,

    [Parameter(Mandatory = $false)]
    [string]$ConfigPath,

    [Parameter(Mandatory = $false)]
    [switch]$ApproveProduction,

    [Parameter(Mandatory = $false)]
    [switch]$AllowUnchangedImage,

    [Parameter(Mandatory = $false)]
    [int]$MinimumAvailableMemoryMb = 1024,

    [Parameter(Mandatory = $false)]
    [int]$MinimumSwapMb = 1024,

    [Parameter(Mandatory = $false)]
    [switch]$SkipResourcePreflight,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Assert-Tooling {
    if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) {
        throw "OpenSSH client (ssh) was not found in PATH."
    }
}

function Invoke-RemoteComposeUpdate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VmUser,
        [Parameter(Mandatory = $true)]
        [string]$VmHost,
        [Parameter(Mandatory = $true)]
        [string]$KeyPath,
        [Parameter(Mandatory = $true)]
        [string]$RemotePath
    )

    $remoteCommand = "cd $RemotePath && docker compose pull && docker compose up -d && docker compose ps"
    $sshTarget = "$VmUser@$VmHost"

    & ssh -i $KeyPath $sshTarget $remoteCommand
    if ($LASTEXITCODE -ne 0) {
        throw "Remote docker compose update failed for '$RemotePath' on '$sshTarget'."
    }
}

function Invoke-BootstrapScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [Parameter(Mandatory = $true)]
        [string]$SshKeyPath,
        [Parameter(Mandatory = $true)]
        [string]$VmHost,
        [Parameter(Mandatory = $true)]
        [string]$VmUser,
        [Parameter(Mandatory = $false)]
        [object[]]$BootstrapArgs
    )

    $invokeArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $ScriptPath,
        "-SshKeyPath", $SshKeyPath,
        "-VmHost", $VmHost,
        "-VmUser", $VmUser
    )

    if ($BootstrapArgs) {
        foreach ($arg in $BootstrapArgs) {
            $invokeArgs += [string]$arg
        }
    }

    & powershell @invokeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Bootstrap script failed: $ScriptPath"
    }
}

function Invoke-HealthCheck {
    param(
        [Parameter(Mandatory = $true)]
        [string]$HealthUrl,
        [Parameter(Mandatory = $true)]
        [int]$ExpectedStatusCode
    )

    $statusText = cmd /c "curl -L -s -o NUL -w ""%{http_code}"" $HealthUrl"
    $statusCode = 0
    [void][int]::TryParse($statusText, [ref]$statusCode)
    if ($statusCode -ne $ExpectedStatusCode) {
        throw "Health check '$HealthUrl' returned HTTP $statusCode. Expected $ExpectedStatusCode."
    }
}

function Invoke-SshCapture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VmUser,
        [Parameter(Mandatory = $true)]
        [string]$VmHost,
        [Parameter(Mandatory = $true)]
        [string]$KeyPath,
        [Parameter(Mandatory = $true)]
        [string]$Command
    )

    $sshTarget = "$VmUser@$VmHost"
    $result = & ssh -i $KeyPath -o BatchMode=yes -o ConnectTimeout=30 -o ServerAliveInterval=10 -o ServerAliveCountMax=2 $sshTarget $Command
    if ($LASTEXITCODE -ne 0) {
        throw "SSH command failed on '$sshTarget'. Command: $Command"
    }

    if ($null -eq $result) {
        return ""
    }

    return [string]::Join([Environment]::NewLine, $result)
}

function Get-RemoteResourceSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VmUser,
        [Parameter(Mandatory = $true)]
        [string]$VmHost,
        [Parameter(Mandatory = $true)]
        [string]$KeyPath
    )

    $remoteCommand = @"
printf 'MEM_AVAILABLE_MB='
awk '/MemAvailable/ { print int(`$2 / 1024) }' /proc/meminfo
printf 'SWAP_TOTAL_MB='
awk '/SwapTotal/ { print int(`$2 / 1024) }' /proc/meminfo
printf 'LOADAVG='
cut -d' ' -f1-3 /proc/loadavg
"@

    $raw = Invoke-SshCapture -VmUser $VmUser -VmHost $VmHost -KeyPath $KeyPath -Command $remoteCommand
    $values = @{}
    foreach ($line in ($raw -split "`r?`n")) {
        if ($line -match "^(?<name>[A-Z_]+)=(?<value>.*)$") {
            $values[$Matches.name] = $Matches.value.Trim()
        }
    }

    return [pscustomobject]@{
        AvailableMemoryMb = [int]$values["MEM_AVAILABLE_MB"]
        SwapTotalMb = [int]$values["SWAP_TOTAL_MB"]
        LoadAverage = [string]$values["LOADAVG"]
    }
}

function Assert-RemoteResourceCapacity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Component,
        [Parameter(Mandatory = $true)]
        [string]$VmUser,
        [Parameter(Mandatory = $true)]
        [string]$VmHost,
        [Parameter(Mandatory = $true)]
        [string]$KeyPath,
        [Parameter(Mandatory = $true)]
        [int]$MinimumAvailableMemoryMb,
        [Parameter(Mandatory = $true)]
        [int]$MinimumSwapMb
    )

    if ($Component -ne "backend") {
        return
    }

    $snapshot = Get-RemoteResourceSnapshot -VmUser $VmUser -VmHost $VmHost -KeyPath $KeyPath
    Write-Host "Remote capacity: available memory $($snapshot.AvailableMemoryMb) MiB, swap $($snapshot.SwapTotalMb) MiB, load $($snapshot.LoadAverage)"

    if ($snapshot.AvailableMemoryMb -lt $MinimumAvailableMemoryMb) {
        throw "Refusing backend deploy: remote available memory is $($snapshot.AvailableMemoryMb) MiB, below required $MinimumAvailableMemoryMb MiB."
    }

    if ($snapshot.SwapTotalMb -lt $MinimumSwapMb) {
        throw "Refusing backend deploy: remote swap is $($snapshot.SwapTotalMb) MiB, below required $MinimumSwapMb MiB."
    }
}

function Get-RemoteServiceImageMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VmUser,
        [Parameter(Mandatory = $true)]
        [string]$VmHost,
        [Parameter(Mandatory = $true)]
        [string]$KeyPath,
        [Parameter(Mandatory = $true)]
        [string]$RemotePath,
        [Parameter(Mandatory = $true)]
        [string]$ServiceName
    )

    $remoteCommand = @"
if [ ! -d $RemotePath ]; then
  exit 0
fi
cd $RemotePath
containerId=`$(docker compose ps -q $ServiceName)
if [ -z "`$containerId" ]; then
  exit 0
fi
imageId=`$(docker inspect "`$containerId" --format '{{.Image}}')
docker image inspect "`$imageId" --format '{{json .Config.Labels}}'
echo
echo "__IMAGE_ID__=`$imageId"
"@

    $raw = Invoke-SshCapture -VmUser $VmUser -VmHost $VmHost -KeyPath $KeyPath -Command $remoteCommand
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }

    $lines = $raw -split "`r?`n"
    $imageIdLine = $lines | Where-Object { $_ -like "__IMAGE_ID__=*" } | Select-Object -First 1
    $labelsJson = (($lines | Where-Object { $_ -and $_ -notlike "__IMAGE_ID__=*" }) -join "")
    $imageId = $null
    if ($imageIdLine) {
        $imageId = $imageIdLine.Substring("__IMAGE_ID__=".Length)
    }

    $labels = $null
    if (-not [string]::IsNullOrWhiteSpace($labelsJson) -and $labelsJson -ne "null") {
        $labels = $labelsJson | ConvertFrom-Json
    }

    return [pscustomobject]@{
        ImageId = $imageId
        Revision = if ($labels) { $labels.'org.opencontainers.image.revision' } else { $null }
        Created = if ($labels) { $labels.'org.opencontainers.image.created' } else { $null }
        Version = if ($labels) { $labels.'org.opencontainers.image.version' } else { $null }
    }
}

function Test-ConfigProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    return $null -ne $InputObject.PSObject.Properties[$PropertyName]
}

function Merge-ConfigObject {
    param(
        [Parameter(Mandatory = $true)]
        [object]$BaseObject,
        [Parameter(Mandatory = $true)]
        [object]$OverrideObject
    )

    foreach ($property in $OverrideObject.PSObject.Properties) {
        $overrideValue = $property.Value
        $baseProperty = $BaseObject.PSObject.Properties[$property.Name]

        if ($null -eq $baseProperty) {
            $BaseObject | Add-Member -NotePropertyName $property.Name -NotePropertyValue $overrideValue
            continue
        }

        $baseValue = $baseProperty.Value
        $isNestedObject =
            $null -ne $baseValue -and
            $null -ne $overrideValue -and
            $baseValue -is [pscustomobject] -and
            $overrideValue -is [pscustomobject]

        if ($isNestedObject) {
            [void](Merge-ConfigObject -BaseObject $baseValue -OverrideObject $overrideValue)
            continue
        }

        $BaseObject.$($property.Name) = $overrideValue
    }

    return $BaseObject
}

function Get-DeploymentConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseConfigPath
    )

    $baseConfig = Get-Content -Raw -LiteralPath $BaseConfigPath | ConvertFrom-Json
    $localOverridePath = [System.IO.Path]::ChangeExtension($BaseConfigPath, ".local.json")

    if (-not (Test-Path -LiteralPath $localOverridePath)) {
        return $baseConfig
    }

    $overrideConfig = Get-Content -Raw -LiteralPath $localOverridePath | ConvertFrom-Json
    return Merge-ConfigObject -BaseObject $baseConfig -OverrideObject $overrideConfig
}

function Get-DefaultComponentsForEnvironment {
    return @("backend", "frontend")
}

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot "..\references\public-environments.json"
}

if (-not (Test-Path -LiteralPath $SshKeyPath)) {
    throw "SSH key file not found: $SshKeyPath"
}

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config file not found: $ConfigPath"
}

Assert-Tooling
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path

$config = Get-DeploymentConfig -BaseConfigPath $ConfigPath
if (-not $config.environments) {
    throw "Invalid config. Missing 'environments' object."
}

$envConfig = $config.environments.$Environment
if (-not $envConfig) {
    $available = ($config.environments.PSObject.Properties.Name | Sort-Object) -join ", "
    throw "Environment '$Environment' not found. Available: $available"
}

if (-not $envConfig.vmHost -or -not $envConfig.vmUser) {
    throw "Environment '$Environment' must define vmHost and vmUser."
}

$requiresExplicitApproval = [bool]$envConfig.requiresExplicitApproval
if ($requiresExplicitApproval -and -not $ApproveProduction -and -not $DryRun) {
    throw "Environment '$Environment' requires explicit approval. Re-run with -ApproveProduction."
}

$requestedComponents = $Components
if (-not $requestedComponents -or $requestedComponents.Count -eq 0) {
    $requestedComponents = Get-DefaultComponentsForEnvironment
}

$normalizedComponents = @(
    $requestedComponents |
        ForEach-Object { $_ -split "," } |
        ForEach-Object { $_.Trim().ToLowerInvariant() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
)
if ($normalizedComponents.Count -eq 0) {
    throw "At least one component must be provided."
}

Write-Host "Environment: $Environment"
Write-Host "VM: $($envConfig.vmUser)@$($envConfig.vmHost)"
Write-Host "Components: $($normalizedComponents -join ', ')"
Write-Host "Dry run: $DryRun"
Write-Host "Allow unchanged image: $AllowUnchangedImage"
Write-Host "Resource preflight: $(-not $SkipResourcePreflight)"

foreach ($component in $normalizedComponents) {
    $componentConfig = $envConfig.components.$component
    if (-not $componentConfig) {
        throw "Component '$component' is not configured for environment '$Environment'."
    }

    if (-not $componentConfig.remotePath) {
        throw "Component '$component' in environment '$Environment' is missing remotePath."
    }

    $serviceName = $null
    if ((Test-ConfigProperty -InputObject $componentConfig -PropertyName "serviceName") -and $componentConfig.serviceName) {
        $serviceName = [string]$componentConfig.serviceName
    }

    Write-Host ""
    Write-Host "==> Deploying component '$component'"
    Write-Host "Remote path: $($componentConfig.remotePath)"

    if (-not $DryRun -and -not $SkipResourcePreflight) {
        Assert-RemoteResourceCapacity -Component $component -VmUser $envConfig.vmUser -VmHost $envConfig.vmHost -KeyPath $SshKeyPath -MinimumAvailableMemoryMb $MinimumAvailableMemoryMb -MinimumSwapMb $MinimumSwapMb
    }

    $beforeMetadata = $null
    if (-not $DryRun -and $serviceName) {
        $beforeMetadata = Get-RemoteServiceImageMetadata -VmUser $envConfig.vmUser -VmHost $envConfig.vmHost -KeyPath $SshKeyPath -RemotePath ([string]$componentConfig.remotePath) -ServiceName $serviceName
        if ($beforeMetadata) {
            Write-Host "Running image before deploy: $($beforeMetadata.ImageId)"
            if ($beforeMetadata.Revision) {
                Write-Host "Running revision before deploy: $($beforeMetadata.Revision)"
            }
        }
        else {
            Write-Host "No running container metadata found before deploy for service '$serviceName'."
        }
    }

    $bootstrapScript = $null
    if ((Test-ConfigProperty -InputObject $componentConfig -PropertyName "bootstrapScript") -and $componentConfig.bootstrapScript) {
        $bootstrapScript = Join-Path $repoRoot ([string]$componentConfig.bootstrapScript)
    }

    if ($bootstrapScript) {
        if (-not (Test-Path -LiteralPath $bootstrapScript)) {
            throw "Bootstrap script not found for '$component': $bootstrapScript"
        }

        if ($DryRun) {
            $argsPreview = @()
            if ((Test-ConfigProperty -InputObject $componentConfig -PropertyName "bootstrapArgs") -and $componentConfig.bootstrapArgs) {
                $argsPreview = @($componentConfig.bootstrapArgs | ForEach-Object { [string]$_ })
            }
            Write-Host "Dry run: would run bootstrap script '$bootstrapScript' with args: $($argsPreview -join ' ')"
        }
        else {
            $bootstrapArgs = $null
            if ((Test-ConfigProperty -InputObject $componentConfig -PropertyName "bootstrapArgs") -and $componentConfig.bootstrapArgs) {
                $bootstrapArgs = $componentConfig.bootstrapArgs
            }

            Invoke-BootstrapScript -ScriptPath $bootstrapScript -SshKeyPath $SshKeyPath -VmHost $envConfig.vmHost -VmUser $envConfig.vmUser -BootstrapArgs $bootstrapArgs
        }
    }
    elseif ($DryRun) {
        Write-Host "Dry run: skipping remote compose update."
    }
    else {
        Invoke-RemoteComposeUpdate -VmUser $envConfig.vmUser -VmHost $envConfig.vmHost -KeyPath $SshKeyPath -RemotePath $componentConfig.remotePath
    }

    if ($componentConfig.healthUrl) {
        $expectedStatusCode = 200
        if ($componentConfig.expectedStatusCode) {
            $expectedStatusCode = [int]$componentConfig.expectedStatusCode
        }

        Write-Host "Health check: $($componentConfig.healthUrl) (expect $expectedStatusCode)"
        if ($DryRun) {
            Write-Host "Dry run: skipping health check."
        }
        else {
            Invoke-HealthCheck -HealthUrl $componentConfig.healthUrl -ExpectedStatusCode $expectedStatusCode
        }
    }
    else {
        Write-Host "No healthUrl configured for '$component'; skipping health check."
    }

    if (-not $DryRun -and $serviceName) {
        $afterMetadata = Get-RemoteServiceImageMetadata -VmUser $envConfig.vmUser -VmHost $envConfig.vmHost -KeyPath $SshKeyPath -RemotePath ([string]$componentConfig.remotePath) -ServiceName $serviceName
        if (-not $afterMetadata) {
            throw "Could not inspect running container metadata after deploy for service '$serviceName'."
        }

        Write-Host "Running image after deploy: $($afterMetadata.ImageId)"
        if ($afterMetadata.Revision) {
            Write-Host "Running revision after deploy: $($afterMetadata.Revision)"
        }
        if ($afterMetadata.Created) {
            Write-Host "Running image created at: $($afterMetadata.Created)"
        }

        if ($beforeMetadata -and $beforeMetadata.ImageId -eq $afterMetadata.ImageId -and -not $AllowUnchangedImage) {
            throw "Running image for component '$component' did not change after deploy. Before and after image id: $($afterMetadata.ImageId). If this was an intentional same-image redeploy, re-run with -AllowUnchangedImage."
        }
    }
}

Write-Host ""
Write-Host "Deployment flow completed."
