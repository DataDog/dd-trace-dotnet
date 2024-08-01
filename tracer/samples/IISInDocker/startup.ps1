# startup.ps1: Startup script to customize the environment when the container starts but before starting IIS

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# For example, set DD_AGENT_HOST at launch time, which may be necessary for ECS applications sending traces to their EC2 instance
# $dockerHost = (curl http://169.254.169.254/latest/meta-data/local-ipv4).Content
# [Environment]::SetEnvironmentVariable("DD_AGENT_HOST", "$dockerHost")

# For example, set DD_AGENT_HOST to the Docker Desktop where the Datadog Agent is running
$dockerHost = (Get-NetIPConfiguration | Where-Object InterfaceAlias -eq "Ethernet").IPv4DefaultGateway.NextHop
[Environment]::SetEnvironmentVariable("DD_AGENT_HOST", "$dockerHost")

# Run 'ServiceMonitor.exe w3svc', just like the aspnet docker image entrypoints
# This will stop and start IIS, so new environment variables added above will take effect
C:\ServiceMonitor.exe w3svc