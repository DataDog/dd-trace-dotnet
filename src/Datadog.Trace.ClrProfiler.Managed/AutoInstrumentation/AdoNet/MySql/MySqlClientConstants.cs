namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.MySql
{
    internal static class MySqlClientConstants
    {
        public const string SqlCommandIntegrationName = "MySqlCommand";

        public static class MySqlData
        {
            public const string AssemblyName = "MySql.Data";
            public const string SqlCommandType = "MySql.Data.MySqlClient.MySqlCommand";
            public const string MinimumVersion = "8.0.0";
            public const string MaximumVersion = "8.*.*";
            public const string SqlDataReaderType = "MySql.Data.MySqlClient.MySqlDataReader";
            public const string SqlDataReaderTaskType = "System.Threading.Tasks.Task`1<MySql.Data.MySqlClient.MySqlDataReader>";

            public class InstrumentSqlCommandAttribute : Datadog.Trace.ClrProfiler.InstrumentMethodAttribute
            {
                public InstrumentSqlCommandAttribute()
                {
                    Assembly = MySqlClientConstants.MySqlData.AssemblyName;
                    Type = MySqlClientConstants.MySqlData.SqlCommandType;
                    MinimumVersion = MySqlClientConstants.MySqlData.MinimumVersion;
                    MaximumVersion = MySqlClientConstants.MySqlData.MaximumVersion;
                    IntegrationName = MySqlClientConstants.SqlCommandIntegrationName;
                }
            }
        }

        public static class MySqlConnector
        {
            public const string AssemblyName = "MySqlConnector";
            public const string SqlCommandType = "MySqlConnector.MySqlCommand";
            public const string MinimumVersion = "1.0.0";
            public const string MaximumVersion = "1.*.*";
            public const string SqlDataReaderType = "MySqlConnector.MySqlDataReader";
            public const string SqlDataReaderTaskType = "System.Threading.Tasks.Task`1<MySqlConnector.MySqlDataReader>";

            public class InstrumentSqlCommandAttribute : Datadog.Trace.ClrProfiler.InstrumentMethodAttribute
            {
                public InstrumentSqlCommandAttribute()
                {
                    Assembly = MySqlClientConstants.MySqlConnector.AssemblyName;
                    Type = MySqlClientConstants.MySqlConnector.SqlCommandType;
                    MinimumVersion = MySqlClientConstants.MySqlConnector.MinimumVersion;
                    MaximumVersion = MySqlClientConstants.MySqlConnector.MaximumVersion;
                    IntegrationName = MySqlClientConstants.SqlCommandIntegrationName;
                }
            }
        }
    }
}
