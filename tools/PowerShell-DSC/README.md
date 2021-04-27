
```powershell
# update DSC resources
Install-Module PSDscResources

# Add function to global scope
. .\Create-DatadogDotnetTracingConfiguration.ps1

# Compile the configuration
DatadogDotnetTracingConfiguration

# To allow DSC to run, Windows needs to be configured to receive PowerShell remote commands
Set-WsManQuickConfig -Force

# Apply the configuration
Start-DscConfiguration -Force -Wait -Verbose -Path .\DatadogDotnetTracingConfiguration\

# Get the current state of the configuration
Get-DscConfiguration
Get-DscConfigurationStatus
Get-DscLocalConfigurationManager

# Pull a new configuration and apply it (in pull mode)
Update-DscConfiguration

# Remove the current configuration
Remove-DscConfigurationDocument -Stage Current -Verbose

# Configure settings in Local Configuration Manager
Set-DSCLocalConfigurationManager -Path 'c:\metaconfig\localhost.meta.mof' -Verbose

# Other
Stop-DscConfiguration
Test-DscConfiguration
```
