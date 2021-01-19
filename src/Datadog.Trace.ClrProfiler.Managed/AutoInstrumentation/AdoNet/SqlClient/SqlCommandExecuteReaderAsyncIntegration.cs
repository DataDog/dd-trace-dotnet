namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.SqlClient
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// Task[SqlDataReader] System.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
    /// Task[SqlDataReader] Microsoft.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
    /// </summary>
    [InstrumentMethod(
        Assemblies = new[] { SqlClientConstants.SystemData.AssemblyName, SqlClientConstants.SystemData.SqlClientAssemblyName },
        Type = SqlClientConstants.SystemData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteReaderAsync,
        ReturnTypeName = SqlClientConstants.SystemData.SqlDataReaderTaskType,
        ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
        MinimumVersion = SqlClientConstants.SystemData.MinimumVersion,
        MaximumVersion = SqlClientConstants.SystemData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    [InstrumentMethod(
        Assembly = SqlClientConstants.MicrosoftData.AssemblyName,
        Type = SqlClientConstants.MicrosoftData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteReaderAsync,
        ReturnTypeName = SqlClientConstants.MicrosoftData.SqlDataReaderTaskType,
        ParametersTypesNames = new[] { AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
        MinimumVersion = SqlClientConstants.MicrosoftData.MinimumVersion,
        MaximumVersion = SqlClientConstants.MicrosoftData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    public class SqlCommandExecuteReaderAsyncIntegration
    {
    }
}
