namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.SqlClient
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// object System.Data.SqlClient.SqlCommand.ExecuteScalar()
    /// object Microsoft.Data.SqlClient.SqlCommand.ExecuteScalar()
    /// </summary>
    [InstrumentMethod(
        Assemblies = new[] { SqlClientConstants.SystemData.AssemblyName, SqlClientConstants.SystemData.SqlClientAssemblyName },
        Type = SqlClientConstants.SystemData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteScalar,
        ReturnTypeName = ClrNames.Object,
        MinimumVersion = SqlClientConstants.SystemData.MinimumVersion,
        MaximumVersion = SqlClientConstants.SystemData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    [InstrumentMethod(
        Assembly = SqlClientConstants.MicrosoftData.AssemblyName,
        Type = SqlClientConstants.MicrosoftData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteScalar,
        ReturnTypeName = ClrNames.Object,
        MinimumVersion = SqlClientConstants.MicrosoftData.MinimumVersion,
        MaximumVersion = SqlClientConstants.MicrosoftData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    public class SqlCommandExecuteScalarIntegration
    {
    }
}
