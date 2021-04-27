Configuration DatadogApmDotnet
{
    param
    (
        # Target nodes to apply the configuration
        [string]$NodeName = 'localhost',

        # Determines whether to install the Agent
        [bool]$InstallAgent = $true,

        # Determines whether to install the Tracer
        [bool]$InstallTracer = $true
    )

    Import-DscResource -ModuleName PSDscResources -Name MsiPackage
    Import-DscResource -ModuleName PSDscResources -Name Environment

    # Version of the Agent package to be installed
    $AgentVersion = '7.27.0'

    # Version of the Tracer package to be installed
    $TracerVersion = '1.26.1'

    Node $NodeName
    {
        # Agent msi installer
        if ($InstallAgent) {
            MsiPackage 'dd-agent' {
                Path      = "https://s3.amazonaws.com/ddagent-windows-stable/ddagent-cli-$AgentVersion.msi"
                ProductId = 'B55FFED6-0CAD-4F94-AA07-5B74A5776C1C'
                Ensure    = 'Present'
            }
        }

        # .NET Tracer msi installer
        if ($InstallTracer) {
            MsiPackage 'dd-trace-dotnet' {
                Path      = "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$TracerVersion/datadog-dotnet-apm-$TracerVersion-x64.msi"
                ProductId = '00B19BDB-EC40-4ADF-A73F-789A7721247A'
                Ensure    = 'Present'
            }

            Environment 'COR_PROFILER' {
                Name   = 'COR_PROFILER'
                Value  = '{846F5F1C-F9AE-4B07-969E-05C26BC060D8}'
                Ensure = 'Present'
                Path   = $false
                Target = @('Machine')
            }

            Environment 'CORECLR_PROFILER' {
                Name   = 'CORECLR_PROFILER'
                Value  = '{846F5F1C-F9AE-4B07-969E-05C26BC060D8}'
                Ensure = 'Present'
                Path   = $false
                Target = @('Machine')
            }
        }
    }
}
