using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.SqlClient
{
    /// <summary>
    /// CallTarget instrumentation for:
    /// SqlDataReader System.Data.SqlClient.SqlCommand.ExecuteReader()
    /// SqlDataReader Microsoft.Data.SqlClient.SqlCommand.ExecuteReader()
    /// </summary>
    [InstrumentMethod(
        Assemblies = new[] { SqlClientConstants.SystemData.AssemblyName, SqlClientConstants.SystemData.SqlClientAssemblyName },
        Type = SqlClientConstants.SystemData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteReader,
        ReturnTypeName = SqlClientConstants.SystemData.SqlDataReaderType,
        MinimumVersion = SqlClientConstants.SystemData.MinimumVersion,
        MaximumVersion = SqlClientConstants.SystemData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    [InstrumentMethod(
        Assembly = SqlClientConstants.MicrosoftData.AssemblyName,
        Type = SqlClientConstants.MicrosoftData.SqlCommandType,
        Method = AdoNetConstants.MethodNames.ExecuteReader,
        ReturnTypeName = SqlClientConstants.MicrosoftData.SqlDataReaderType,
        MinimumVersion = SqlClientConstants.MicrosoftData.MinimumVersion,
        MaximumVersion = SqlClientConstants.MicrosoftData.MaximumVersion,
        IntegrationName = SqlClientConstants.SqlCommandIntegrationName)]
    public class SqlCommandExecuteReaderIntegration
    {
    }
}
