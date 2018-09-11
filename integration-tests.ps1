Param(
  [string]$Platform,
  [string]$Configuration
)

docker-compose up --detach
try
{
  $Env:CONNECTION_STRING="Data Source=127.0.0.1,1433;Initial Catalog=Trace;Integrated Security=False;MultipleActiveResultSets=True;User Id=sa;Password=password!123"
  $frameworks = @("net47")
  $name = "Datadog.Trace.ClrProfiler.IntegrationTests"

  foreach ($framework in $frameworks) {
    $dll = Join-Path "test" $name "bin" $Platform $Configuration $framework "$($name).dll"
    dotnet vstest /Platform:$Platform /Logger:trx $dll
  }
}
finally
{
  docker-compose down
}
