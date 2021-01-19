namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.SqlClient
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// Task[int] System.Data.SqlClient.SqlCommand.ExecuteNonQueryAsync(CancellationToken)
    /// Task[int] Microsoft.Data.SqlClient.SqlCommand.ExecuteNonQueryAsync(CancellationToken)
    /// </summary>
    [InstrumentMethod(
        Assemblies = new[] { SqlClientConstants.SystemData.AssemblyName, SqlClientConstants.SystemData.SqlClientAssemblyName },
        Type = SqlClientConstants.SystemData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteNonQueryAsync,
        ReturnTypeName = "System.Threading.Tasks.Task`1<System.Int32>",
        ParametersTypesNames = new[] { ClrNames.CancellationToken },
        MinimumVersion = SqlClientConstants.SystemData.MinimumVersion,
        MaximumVersion = SqlClientConstants.SystemData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    [InstrumentMethod(
        Assembly = SqlClientConstants.MicrosoftData.AssemblyName,
        Type = SqlClientConstants.MicrosoftData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteNonQueryAsync,
        ReturnTypeName = "System.Threading.Tasks.Task`1<System.Int32>",
        ParametersTypesNames = new[] { ClrNames.CancellationToken },
        MinimumVersion = SqlClientConstants.MicrosoftData.MinimumVersion,
        MaximumVersion = SqlClientConstants.MicrosoftData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    public class SqlCommandExecuteNonQueryAsyncIntegration
    {
    }
}
