## Preparing the dev environment

```powershell

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Update PowerShellGet
Install-Module PowerShellGet

# Update NuGet provider for PowerShellGet
Install-PackageProvider -Name NuGet

# Install updated DSC resources
Install-Module -Name PSDscResources -Repository PSGallery -Force
```

## Compiling the DSC Configuration

```powershell
# Add the Configuration function to global scope
. .\DatadogApmDotnet.ps1

# Compile the Configuration by calling the function
DatadogApmDotnet
```

## Applying the DSC Configuration

```powershell
# To allow DSC to run, Windows needs to be configured to receive PowerShell remote commands
Set-WsManQuickConfig -Force

# Apply the Configuration
Start-DscConfiguration -Force -Wait -Verbose -Path .\DatadogApmDotnet\

# Get the current state of the configuration
Get-DscConfiguration
Get-DscConfigurationStatus
Get-DscLocalConfigurationManager

# Pull a new configuration and apply it (in pull mode)
Update-DscConfiguration

# Remove the current configuration
Remove-DscConfigurationDocument -Stage Current -Verbose

# Configure settings in Local Configuration Manager
Set-DSCLocalConfigurationManager -Verbose -Path 'c:\metaconfig\localhost.meta.mof'

# Other commands
Stop-DscConfiguration
Test-DscConfiguration
```
