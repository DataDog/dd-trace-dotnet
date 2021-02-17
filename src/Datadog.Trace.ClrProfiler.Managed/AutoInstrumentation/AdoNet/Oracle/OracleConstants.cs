namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Oracle
{
    internal static class OracleConstants
    {
        public const string SqlCommandIntegrationName = "OracleCommand";

        internal struct OracleClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "Oracle.ManagedDataAccess";

            public string SqlCommandType => "Oracle.ManagedDataAccess.Client.OracleCommand";

            public string MinimumVersion => "4.122.0";

            public string MaximumVersion => "4.122.*";

            public string DataReaderType => "Oracle.ManagedDataAccess.Client.OracleDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<Oracle.ManagedDataAccess.Client.OracleDataReader>";
        }

        internal struct OracleCoreClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "Oracle.ManagedDataAccess";

            public string SqlCommandType => "Oracle.ManagedDataAccess.Client.OracleCommand";

            public string MinimumVersion => "2.0.0";

            public string MaximumVersion => "2.*.*";

            public string DataReaderType => "Oracle.ManagedDataAccess.Client.OracleDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<Oracle.ManagedDataAccess.Client.OracleDataReader>";
        }

        internal struct OracleDataAccessClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "Oracle.DataAccess";

            public string SqlCommandType => "Oracle.DataAccess.Client.OracleCommand";

            public string MinimumVersion => "4.122.0";

            public string MaximumVersion => "4.122.*";

            public string DataReaderType => "Oracle.DataAccess.Client.OracleDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<Oracle.DataAccess.Client.OracleDataReader>";
        }
    }
}
