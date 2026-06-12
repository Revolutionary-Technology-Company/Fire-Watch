<#
.SYNOPSIS
    Automated Deployment Script for Genetec Edwards FireWatch Bridge Service.
.DESCRIPTION
    This script handles directory structures, service registration, advanced recovery 
    hardening configurations, and local Windows Firewall rules.
.NOTES
    Must be executed with elevated Administrative privileges on the host hospital server.
#>

# 1. Enforce Elevated Administrative Execution Check
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "CRITICAL: This script must be executed from an elevated PowerShell instance (Run as Administrator)."
    Exit
}

# 2. Configuration Parameters - Update these paths to match your server layout
$ServiceName        = "GenetecEdwardsFireWatch"
$ServiceDisplayName = "Genetec Edwards FireWatch Bridge"
$ServiceDescription = "Monitors live video analytics events from Genetec Security Center and dynamically translates/forwards visual fire alerts to the Edwards FireWorks incident management workstation platform."
$TargetDirectory    = "C:\HospitalApps\FireWatch"
$ExecutableName     = "GenetecEdwardsBridge.exe"
$FullBinaryPath     = Join-Path $TargetDirectory $ExecutableName
$ConfigName         = "appsettings.json"
$FullConfigPath     = Join-Path $TargetDirectory $ConfigName

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " INITIALIZING DEPLOYMENT: $ServiceDisplayName" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

# 3. Environment & Dependency Validation Checks
if (-not (Test-Path $TargetDirectory)) {
    Write-Host "[*] Target folder missing. Creating directory tracking branch..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $TargetDirectory | Out-Null
}

if (-not (Test-Path $FullBinaryPath)) {
    Write-Error "DEPLOYMENT ABORTED: The target executable binary could not be found at: $FullBinaryPath"
    Write-Host "Please compile your project and drop '$ExecutableName' into the target folder before running this script again." -ForegroundColor Yellow
    Exit
}

if (-not (Test-Path $FullConfigPath)) {
    Write-Warning "][!] Configuration file template '$ConfigName' missing from execution root path."
    Write-Host "[*] Creating an empty fallback configuration template object now..." -ForegroundColor Yellow
    $DefaultConfigTemplate = @{
        GenetecConfig = @{ DirectoryServer = "127.0.0.1"; ServiceUser = "User"; ServicePassword = "Password"; KiwiFireEventGuid = "00000000-0000-0000-0000-000000000000" }
        EdwardsConfig = @{ ReceiverIp = "127.0.0.1"; ReceiverPort = 2323; HeartbeatIntervalSeconds = 60 }
        HospitalMap   = @()
    }
    $DefaultConfigTemplate | ConvertTo-Json -Depth 4 | Out-File $FullConfigPath -Encoding utf8
}

# 4. Safe Replacement Handling for Pre-Existing Services
$ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($ExistingService) {
    Write-Host "[*] Pre-existing service instance detected. Halting active threads safely..." -ForegroundColor Yellow
    if ($ExistingService.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    Write-Host "[*] Unregistering historical database configuration definitions..." -ForegroundColor Yellow
    $null = & sc.exe delete $ServiceName
    Start-Sleep -Seconds 1
}

# 5. Native Windows Service Registration Action
Write-Host "[*] Registering service with Windows Service Control Manager..." -ForegroundColor Gray
$ScArgs = @(
    "create", $ServiceName,
    "binPath=", "`"$FullBinaryPath`"",
    "start=", "auto",
    "DisplayName=", "`"$ServiceDisplayName`""
)
$CreateResult = & sc.exe $ScArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "CRITICAL FAILURE: Windows SCM registration pipeline threw an exception code. Code output: $CreateResult"
    Exit
}

# 6. Apply Service Description Metadata
$null = & sc.exe description $ServiceName $ServiceDescription

# 7. Harden Advanced Failure Recovery Actions
Write-Host "[*] Configuring life-safety automated crash recovery parameters..." -ForegroundColor Gray
# reset=86400 resets the counter daily. actions=restart/delay_ms/restart/delay_ms/...
$FailureArgs = @(
    "failure", $ServiceName,
    "reset=", "86400",
    "actions=", "restart/5000/restart/10000/restart/30000"
)
$null = & sc.exe $FailureArgs

# 8. Automated Windows Firewall Provisioning Rule Sets
Write-Host "[*] Checking network firewall accessibility policy parameters..." -ForegroundColor Gray
$FirewallRuleName = "GenetecEdwardsBridge-Outbound"
$ExistingRule = Get-NetFirewallRule -Name $FirewallRuleName -ErrorAction SilentlyContinue

if (-not $ExistingRule) {
    Write-Host "[*] Injecting specialized persistent outbound security exception rule set..." -ForegroundColor Yellow
    New-NetFirewallRule -Name $FirewallRuleName `
                        -DisplayName "Genetec Edwards FireWatch Bridge (Outbound)" `
                        -Description "Allows outbound supplemental fire event reporting telemetry transmission signals to flow to network endpoints." `
                        -Direction Outbound `
                        -Program $FullBinaryPath `
                        -Action Allow `
                        -Enabled True | Out-Null
}

# 9. Execution Verification Step Loop
Write-Host "[*] Attempting system initialization execution sequences..." -ForegroundColor Gray
Start-Service -Name $ServiceName
Start-Sleep -Seconds 2

$FinalCheck = Get-Service -Name $ServiceName
if ($FinalCheck.Status -eq 'Running') {
    Write-Host "`n=====================================================================" -ForegroundColor Green
    Write-Host " DEPLOYMENT SUCCESSFUL: Bridge service is active and running." -ForegroundColor Green
    Write-Host "=====================================================================" -ForegroundColor Green
} else {
    Write-Error "DEPLOYMENT WARNING: Service registered but failed to start cleanly. Check the Windows Event Viewer (Application log source: '$ServiceName') for specific boot crash analytics tracing variables."
}
