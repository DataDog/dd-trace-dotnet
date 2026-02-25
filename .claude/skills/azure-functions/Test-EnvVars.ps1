#!/usr/bin/env pwsh
#Requires -Version 5.1

<#
.SYNOPSIS
    Verifies Datadog instrumentation environment variables on an Azure Function App.

.DESCRIPTION
    Checks that required (and optionally recommended) environment variables are configured
    correctly for Datadog instrumentation. Detects platform (Linux/Windows) and validates
    platform-specific profiler paths. Also checks function app state.

    SECURITY: DD_API_KEY existence is checked but its value is NEVER retrieved or displayed.

.PARAMETER AppName
    The Azure Function App name (required).

.PARAMETER ResourceGroup
    The Azure resource group containing the app (required).

.PARAMETER IncludeRecommended
    Also validate recommended variables (feature disables, etc.).

.OUTPUTS
    PSCustomObject with AppName, Platform, State, RequiredVars, RecommendedVars,
    AllRequiredPresent, and Issues.

.EXAMPLE
    ./.claude/skills/azure-functions/Test-EnvVars.ps1 -AppName "my-func" -ResourceGroup "my-rg"

.EXAMPLE
    ./.claude/skills/azure-functions/Test-EnvVars.ps1 -AppName "my-func" -ResourceGroup "my-rg" -IncludeRecommended
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$AppName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [switch]$IncludeRecommended
)

$ErrorActionPreference = "Stop"

# --- Helper: colored console output ---
function Write-Result {
    param(
        [string]$Label,
        [string]$Status,  # PASS, FAIL, WARN, INFO
        [string]$Detail = ""
    )

    $color = switch ($Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "WARN" { "Yellow" }
        "INFO" { "Cyan" }
        default { "White" }
    }

    $prefix = "[$Status]"
    $message = "$prefix $Label"
    if ($Detail) { $message += " - $Detail" }
    Write-Host $message -ForegroundColor $color
}

# --- 1. Check function app state and detect platform ---
Write-Host ""
Write-Host "=== Checking Function App: $AppName ===" -ForegroundColor Cyan
Write-Host ""

try {
    $appInfo = az functionapp show `
        --name $AppName `
        --resource-group $ResourceGroup `
        --query "{state:state, kind:kind}" `
        -o json 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to query function app '$AppName' in resource group '$ResourceGroup': $appInfo"
        exit 1
    }

    $appInfo = $appInfo | ConvertFrom-Json
}
catch {
    Write-Error "Failed to query function app: $_"
    exit 1
}

$appState = $appInfo.state
$appKind = $appInfo.kind

# Detect platform from "kind" field (e.g., "functionapp,linux" or "functionapp")
$isLinuxApp = $appKind -match "linux"
$platform = if ($isLinuxApp) { "linux" } else { "windows" }

Write-Result "App State" $(if ($appState -eq "Running") { "PASS" } else { "FAIL" }) $appState
Write-Result "Platform" "INFO" "$platform (kind: $appKind)"
Write-Host ""

# --- 2. Fetch all app settings (excluding DD_API_KEY value) ---
# SECURITY: Query DD_API_KEY existence separately to never retrieve its value
try {
    # Fetch all settings EXCEPT DD_API_KEY
    $settingsJson = az functionapp config appsettings list `
        --name $AppName `
        --resource-group $ResourceGroup `
        --query "[?name!='DD_API_KEY']" `
        -o json 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to fetch app settings: $settingsJson"
        exit 1
    }

    $allSettings = $settingsJson | ConvertFrom-Json

    # Separately check DD_API_KEY existence (never retrieving its value)
    $apiKeyExists = az functionapp config appsettings list `
        --name $AppName `
        --resource-group $ResourceGroup `
        --query "[?name=='DD_API_KEY'].name" `
        -o tsv 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to check DD_API_KEY existence: $apiKeyExists"
        exit 1
    }
}
catch {
    Write-Error "Failed to fetch app settings: $_"
    exit 1
}

# Build a hashtable for quick lookup
$settingsMap = @{}
foreach ($s in $allSettings) {
    $settingsMap[$s.name] = $s.value
}

# Add DD_API_KEY existence check result (value never retrieved)
if ($apiKeyExists -eq "DD_API_KEY") {
    $settingsMap["DD_API_KEY"] = "(set)"
}

# --- 3. Define required and recommended variables ---

# Common required vars (platform-independent)
$requiredVars = [ordered]@{
    "CORECLR_ENABLE_PROFILING"  = @{ ExpectedValue = "1"; Description = "Enables CLR profiling API" }
    "CORECLR_PROFILER"          = @{ ExpectedValue = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"; Description = "Datadog profiler GUID" }
    "DD_DOTNET_TRACER_HOME"     = @{ ExpectedValue = $null; Description = "Tracer managed assemblies directory" }
    "DD_API_KEY"                = @{ ExpectedValue = $null; Description = "Datadog API key" }
    "DOTNET_STARTUP_HOOKS"      = @{ ExpectedValue = $null; Description = "Serverless compat startup hook" }
}

# Platform-specific profiler path vars
if ($isLinuxApp) {
    $requiredVars["CORECLR_PROFILER_PATH"] = @{
        ExpectedValue = "/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
        Description   = "Native profiler path (Linux)"
    }
}
else {
    $requiredVars["CORECLR_PROFILER_PATH_32"] = @{
        ExpectedValue = 'C:\home\site\wwwroot\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll'
        Description   = "Native profiler path (Windows 32-bit)"
    }
    $requiredVars["CORECLR_PROFILER_PATH_64"] = @{
        ExpectedValue = 'C:\home\site\wwwroot\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll'
        Description   = "Native profiler path (Windows 64-bit)"
    }
}

$recommendedVars = [ordered]@{
    "DD_APPSEC_ENABLED"                 = @{ ExpectedValue = "false"; Description = "Disable AppSec (unsupported in Functions)" }
    "DD_CIVISIBILITY_ENABLED"           = @{ ExpectedValue = "false"; Description = "Disable CI Visibility (unsupported in Functions)" }
    "DD_REMOTE_CONFIGURATION_ENABLED"   = @{ ExpectedValue = "false"; Description = "Disable Remote Configuration" }
    "DD_AGENT_FEATURE_POLLING_ENABLED"  = @{ ExpectedValue = "false"; Description = "Disable Agent feature polling" }
    "DD_TRACE_Process_ENABLED"          = @{ ExpectedValue = "false"; Description = "Disable Process instrumentation (reduces noise)" }
}

# --- 4. Validate required variables ---
Write-Host "--- Required Variables ---" -ForegroundColor Cyan

$issues = @()
$requiredResults = [ordered]@{}

foreach ($varName in $requiredVars.Keys) {
    $spec = $requiredVars[$varName]
    $actualValue = $settingsMap[$varName]

    if ($null -eq $actualValue -or $actualValue -eq "") {
        Write-Result $varName "FAIL" "NOT SET - $($spec.Description)"
        $requiredResults[$varName] = @{ Status = "FAIL"; Value = "(not set)"; Expected = $spec.ExpectedValue }
        $issues += "$varName is not set"
    }
    elseif ($varName -eq "DD_API_KEY") {
        # Never compare value, just confirm existence
        Write-Result $varName "PASS" "(set)"
        $requiredResults[$varName] = @{ Status = "PASS"; Value = "(set)"; Expected = $null }
    }
    elseif ($null -ne $spec.ExpectedValue -and $actualValue -ne $spec.ExpectedValue) {
        Write-Result $varName "FAIL" "Expected '$($spec.ExpectedValue)', got '$actualValue'"
        $requiredResults[$varName] = @{ Status = "FAIL"; Value = $actualValue; Expected = $spec.ExpectedValue }
        $issues += "$varName has incorrect value (expected '$($spec.ExpectedValue)', got '$actualValue')"
    }
    else {
        $displayValue = if ($spec.ExpectedValue) { $actualValue } else { $actualValue }
        Write-Result $varName "PASS" $displayValue
        $requiredResults[$varName] = @{ Status = "PASS"; Value = $actualValue; Expected = $spec.ExpectedValue }
    }
}

$allRequiredPresent = ($issues.Count -eq 0)

# --- 5. Validate recommended variables (if requested) ---
$recommendedResults = [ordered]@{}

if ($IncludeRecommended) {
    Write-Host ""
    Write-Host "--- Recommended Variables ---" -ForegroundColor Cyan

    foreach ($varName in $recommendedVars.Keys) {
        $spec = $recommendedVars[$varName]
        $actualValue = $settingsMap[$varName]

        if ($null -eq $actualValue -or $actualValue -eq "") {
            Write-Result $varName "WARN" "NOT SET - $($spec.Description)"
            $recommendedResults[$varName] = @{ Status = "WARN"; Value = "(not set)"; Expected = $spec.ExpectedValue }
        }
        elseif ($null -ne $spec.ExpectedValue -and $actualValue -ne $spec.ExpectedValue) {
            Write-Result $varName "WARN" "Expected '$($spec.ExpectedValue)', got '$actualValue'"
            $recommendedResults[$varName] = @{ Status = "WARN"; Value = $actualValue; Expected = $spec.ExpectedValue }
        }
        else {
            Write-Result $varName "PASS" $actualValue
            $recommendedResults[$varName] = @{ Status = "PASS"; Value = $actualValue; Expected = $spec.ExpectedValue }
        }
    }
}

# --- 6. Summary ---
Write-Host ""
if ($appState -ne "Running") {
    Write-Host "WARNING: Function app is '$appState' (not Running). Start it before testing." -ForegroundColor Yellow
    $issues += "Function app state is '$appState'"
}

if ($allRequiredPresent) {
    Write-Host "All required variables are configured correctly." -ForegroundColor Green
}
else {
    Write-Host "ISSUES FOUND: $($issues.Count) problem(s) detected." -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "  - $issue" -ForegroundColor Red
    }
}

Write-Host ""

# --- 7. Return structured result ---
return [PSCustomObject]@{
    AppName            = $AppName
    Platform           = $platform
    State              = $appState
    RequiredVars       = $requiredResults
    RecommendedVars    = $recommendedResults
    AllRequiredPresent = $allRequiredPresent
    Issues             = $issues
}
