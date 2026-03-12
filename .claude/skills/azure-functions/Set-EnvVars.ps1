#!/usr/bin/env pwsh
#Requires -Version 5.1

<#
.SYNOPSIS
    Sets Datadog instrumentation environment variables on an Azure Function App.

.DESCRIPTION
    Configures required, recommended, and/or debug environment variables for Datadog
    instrumentation. Detects platform (Linux/Windows) and sets platform-specific profiler paths.

    SECURITY: DD_API_KEY is passed as a parameter and set via Azure CLI, but never logged or displayed.

.PARAMETER AppName
    The Azure Function App name (required).

.PARAMETER ResourceGroup
    The Azure resource group containing the app (required).

.PARAMETER ApiKey
    The Datadog API key (required). Never logged or displayed.

.PARAMETER Tier
    Configuration tier: "required", "recommended", or "debug".
    - required: Minimum variables for instrumentation
    - recommended: required + feature disables + DD_ENV
    - debug: recommended + debug logging + direct log submission
    Default: "required"

.PARAMETER Env
    Value for DD_ENV (e.g., "dev-lucas", "staging"). Only set if provided.

.PARAMETER Service
    Value for DD_SERVICE. Only set if provided.

.PARAMETER Version
    Value for DD_VERSION. Only set if provided.

.PARAMETER SamplingRules
    Value for DD_TRACE_SAMPLING_RULES (JSON string). Only set if provided.

.PARAMETER ExtraSettings
    Hashtable of additional settings to set (e.g., @{"DD_TRACE_HEADER_TAGS"="x-request-id:request_id"}).

.PARAMETER SkipRestart
    Skip restarting the function app after setting variables.

.OUTPUTS
    PSCustomObject with AppName, Platform, SettingsApplied, and Restarted.

.EXAMPLE
    ./.claude/skills/azure-functions/Set-EnvVars.ps1 -AppName "my-func" -ResourceGroup "my-rg" -ApiKey "abc123"

.EXAMPLE
    ./.claude/skills/azure-functions/Set-EnvVars.ps1 -AppName "my-func" -ResourceGroup "my-rg" -ApiKey "abc123" -Tier recommended -Env "dev-lucas"

.EXAMPLE
    ./.claude/skills/azure-functions/Set-EnvVars.ps1 -AppName "my-func" -ResourceGroup "my-rg" -ApiKey "abc123" -Tier debug -WhatIf
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [string]$AppName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$ApiKey,

    [ValidateSet("required", "recommended", "debug")]
    [string]$Tier = "required",

    [string]$Env,

    [string]$Service,

    [string]$Version,

    [string]$SamplingRules,

    [hashtable]$ExtraSettings,

    [switch]$SkipRestart
)

$ErrorActionPreference = "Stop"

# --- 1. Detect platform ---
Write-Host ""
Write-Host "=== Configuring Function App: $AppName ===" -ForegroundColor Cyan
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

$isLinuxApp = $appInfo.kind -match "linux"
$platform = if ($isLinuxApp) { "linux" } else { "windows" }

Write-Host "Platform: $platform (kind: $($appInfo.kind))" -ForegroundColor Cyan
Write-Host "Tier: $Tier" -ForegroundColor Cyan
Write-Host ""

# --- 2. Build settings based on tier and platform ---
$settings = [ordered]@{}

# Required variables (always included)
$settings["CORECLR_ENABLE_PROFILING"] = "1"
$settings["CORECLR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
$settings["DD_API_KEY"] = $ApiKey

if ($isLinuxApp) {
    $settings["CORECLR_PROFILER_PATH"] = "/home/site/wwwroot/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
    $settings["DD_DOTNET_TRACER_HOME"] = "/home/site/wwwroot/datadog"
    $settings["DOTNET_STARTUP_HOOKS"] = "/home/site/wwwroot/Datadog.Serverless.Compat.dll"
}
else {
    $settings["CORECLR_PROFILER_PATH_32"] = 'C:\home\site\wwwroot\datadog\win-x86\Datadog.Trace.ClrProfiler.Native.dll'
    $settings["CORECLR_PROFILER_PATH_64"] = 'C:\home\site\wwwroot\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll'
    $settings["DD_DOTNET_TRACER_HOME"] = 'C:\home\site\wwwroot\datadog'
    $settings["DOTNET_STARTUP_HOOKS"] = 'C:\home\site\wwwroot\Datadog.Serverless.Compat.dll'
}

# Recommended variables (tier >= recommended)
if ($Tier -eq "recommended" -or $Tier -eq "debug") {
    $settings["DD_APPSEC_ENABLED"] = "false"
    $settings["DD_CIVISIBILITY_ENABLED"] = "false"
    $settings["DD_REMOTE_CONFIGURATION_ENABLED"] = "false"
    $settings["DD_AGENT_FEATURE_POLLING_ENABLED"] = "false"
    $settings["DD_TRACE_Process_ENABLED"] = "false"
}

# Debug variables (tier == debug)
if ($Tier -eq "debug") {
    $settings["DD_TRACE_DEBUG"] = "true"
    $settings["DD_TRACE_LOG_SINKS"] = "file,console-experimental"
    $settings["DD_LOG_LEVEL"] = "debug"
    $settings["DD_LOGS_DIRECT_SUBMISSION_AZURE_FUNCTIONS_HOST_ENABLED"] = "true"
    $settings["DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS"] = "ILogger"
}

# Optional user-provided values
if ($Env) { $settings["DD_ENV"] = $Env }
if ($Service) { $settings["DD_SERVICE"] = $Service }
if ($Version) { $settings["DD_VERSION"] = $Version }
if ($SamplingRules) { $settings["DD_TRACE_SAMPLING_RULES"] = $SamplingRules }

# Extra settings
if ($ExtraSettings) {
    foreach ($key in $ExtraSettings.Keys) {
        $settings[$key] = $ExtraSettings[$key]
    }
}

# --- 3. Display what will be set ---
Write-Host "--- Settings to apply ---" -ForegroundColor Cyan

foreach ($key in $settings.Keys) {
    if ($key -eq "DD_API_KEY") {
        Write-Host "  $key = (hidden)" -ForegroundColor White
    }
    else {
        Write-Host "  $key = $($settings[$key])" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "Total: $($settings.Count) variable(s)" -ForegroundColor Cyan
Write-Host ""

# --- 4. Apply settings via Azure CLI ---
$restarted = $false

if ($PSCmdlet.ShouldProcess("$AppName ($platform)", "Set $($settings.Count) app settings")) {
    Write-Host "Applying settings..." -ForegroundColor Cyan

    # Build the --settings arguments as an array
    $settingsArgs = @()
    foreach ($key in $settings.Keys) {
        $settingsArgs += "$key=$($settings[$key])"
    }

    try {
        $result = az functionapp config appsettings set `
            --name $AppName `
            --resource-group $ResourceGroup `
            --settings @settingsArgs `
            -o json 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to set app settings: $result"
            exit 1
        }

        Write-Host "Settings applied successfully." -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to set app settings: $_"
        exit 1
    }

    # --- 5. Restart (unless skipped) ---
    if (-not $SkipRestart) {
        if ($PSCmdlet.ShouldProcess($AppName, "Restart function app")) {
            Write-Host ""
            Write-Host "Restarting function app..." -ForegroundColor Cyan

            try {
                $restartResult = az functionapp restart `
                    --name $AppName `
                    --resource-group $ResourceGroup 2>&1

                if ($LASTEXITCODE -ne 0) {
                    Write-Host "WARNING: Restart failed: $restartResult" -ForegroundColor Yellow
                }
                else {
                    $restarted = $true
                    Write-Host "Function app restarted." -ForegroundColor Green
                }
            }
            catch {
                Write-Host "WARNING: Restart failed: $_" -ForegroundColor Yellow
            }
        }
    }
}

# --- 6. Summary ---
Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "App: $AppName"
Write-Host "Platform: $platform"
Write-Host "Variables set: $($settings.Count)"
Write-Host "Restarted: $restarted"
Write-Host ""

return [PSCustomObject]@{
    AppName         = $AppName
    Platform        = $platform
    SettingsApplied = $settings.Keys | Where-Object { $_ -ne "DD_API_KEY" }
    Restarted       = $restarted
}
