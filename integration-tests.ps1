docker-compose up --detach
try
{

$Env:CONNECTION_STRING="Data Source=127.0.0.1,1433;Initial Catalog=Trace;Integrated Security=False;MultipleActiveResultSets=True;User Id=sa;Password=password!123"

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

}
finally
{
    docker-compose down
}
