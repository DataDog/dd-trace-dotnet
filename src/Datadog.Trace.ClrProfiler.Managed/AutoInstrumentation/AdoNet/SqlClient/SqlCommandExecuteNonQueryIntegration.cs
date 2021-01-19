namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.SqlClient
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// int System.Data.SqlClient.SqlCommand.ExecuteNonQuery()
    /// int Microsoft.Data.SqlClient.SqlCommand.ExecuteNonQuery()
    /// </summary>
    [InstrumentMethod(
        Assemblies = new[] { SqlClientConstants.SystemData.AssemblyName, SqlClientConstants.SystemData.SqlClientAssemblyName },
        Type = SqlClientConstants.SystemData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteNonQuery,
        ReturnTypeName = ClrNames.Int32,
        MinimumVersion = SqlClientConstants.SystemData.MinimumVersion,
        MaximumVersion = SqlClientConstants.SystemData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    [InstrumentMethod(
        Assembly = SqlClientConstants.MicrosoftData.AssemblyName,
        Type = SqlClientConstants.MicrosoftData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteNonQuery,
        ReturnTypeName = ClrNames.Int32,
        MinimumVersion = SqlClientConstants.MicrosoftData.MinimumVersion,
        MaximumVersion = SqlClientConstants.MicrosoftData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    public class SqlCommandExecuteNonQueryIntegration
    {
    }
}
