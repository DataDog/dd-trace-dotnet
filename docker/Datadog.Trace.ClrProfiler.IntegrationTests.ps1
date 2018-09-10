$platforms = @("x64", "x86")
$configurations = @("Debug", "Release")
$frameworks = @("net47")
$name = "Datadog.Trace.ClrProfiler.IntegrationTests"

foreach ($platform in $platforms) {
    foreach ($configuration in $configurations) {
        foreach ($framework in $frameworks) {
            dotnet vstest \
                /Platform:"$($platform)" \
                /Logger:trx \
                "test\\$($name)\\bin\\$($platform)\\$($configuration)\\$($framework)\\$($name).dll"
        }
    }
}


