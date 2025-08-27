#
# Installs .NET Runtime 3.5, used by Wix 3.11 and the Visual C++ Compiler for Python 2.7
#
$ErrorActionPreference = "Stop"

# Enable Windows Update service
Set-Service -Name wuauserv -StartupType Automatic
Start-Service -Name wuauserv

# Install .NET Framework 3.5
dism /online /enable-feature /FeatureName:Netfx3 /all

# Disable Windows Update service
Stop-Service -Name wuauserv
Set-Service -Name wuauserv -StartupType Disabled
