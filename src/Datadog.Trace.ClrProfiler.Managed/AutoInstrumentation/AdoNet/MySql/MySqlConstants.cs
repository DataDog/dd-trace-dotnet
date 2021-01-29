namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.MySql
{
    internal static class MySqlConstants
    {
        public const string SqlCommandIntegrationName = "MySqlCommand";

        internal struct MySqlDataClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "MySql.Data";

            public string SqlCommandType => "MySql.Data.MySqlClient.MySqlCommand";

            public string MinimumVersion => "8.0.0";

            public string MaximumVersion => "8.*.*";

            public string DataReaderType => "MySql.Data.MySqlClient.MySqlDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<MySql.Data.MySqlClient.MySqlDataReader>";
        }

        internal struct MySqlConnectorClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "MySqlConnector";

            public string SqlCommandType => "MySqlConnector.MySqlCommand";

            public string MinimumVersion => "1.0.0";

            public string MaximumVersion => "1.*.*";

            public string DataReaderType => "MySqlConnector.MySqlDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<MySqlConnector.MySqlDataReader>";
        }
    }
}
