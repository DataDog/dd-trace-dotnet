namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.SqlClient
{
    internal static class SqlClientConstants
    {
        public const string SqlCommandIntegrationName = "SqlCommand";

        public static class SystemData
        {
            public const string AssemblyName = "System.Data";
            public const string SqlCommandType = "System.Data.SqlClient.SqlCommand";
            public const string MinimumVersion = "4.0.0";
            public const string MaximumVersion = "4.*.*";
            public const string SqlDataReaderType = "System.Data.SqlClient.SqlDataReader";
            public const string SqlDataReaderTaskType = "System.Threading.Tasks.Task`1<System.Data.SqlClient.SqlDataReader>";

            public class InstrumentSqlCommandAttribute : Datadog.Trace.ClrProfiler.InstrumentMethodAttribute
            {
                public InstrumentSqlCommandAttribute()
                {
                    Assemblies = new[] { SqlClientConstants.SystemData.AssemblyName, SqlClientConstants.SystemDataSqlClient.AssemblyName };
                    Type = SqlClientConstants.SystemData.SqlCommandType;
                    MinimumVersion = SqlClientConstants.SystemData.MinimumVersion;
                    MaximumVersion = SqlClientConstants.SystemData.MaximumVersion;
                    IntegrationName = SqlClientConstants.SqlCommandIntegrationName;
                }
            }
        }

        public static class SystemDataSqlClient
        {
            public const string AssemblyName = "System.Data.SqlClient";
        }

        public static class MicrosoftDataSqlClient
        {
            public const string AssemblyName = "Microsoft.Data.SqlClient";
            public const string SqlCommandType = "Microsoft.Data.SqlClient.SqlCommand";
            public const string MinimumVersion = "1.0.0";
            public const string MaximumVersion = "2.*.*";
            public const string SqlDataReaderType = "Microsoft.Data.SqlClient.SqlDataReader";
            public const string SqlDataReaderTaskType = "System.Threading.Tasks.Task`1<Microsoft.Data.SqlClient.SqlDataReader>";

            public class InstrumentSqlCommandAttribute : Datadog.Trace.ClrProfiler.InstrumentMethodAttribute
            {
                public InstrumentSqlCommandAttribute()
                {
                    Assembly = SqlClientConstants.MicrosoftDataSqlClient.AssemblyName;
                    Type = SqlClientConstants.MicrosoftDataSqlClient.SqlCommandType;
                    MinimumVersion = SqlClientConstants.MicrosoftDataSqlClient.MinimumVersion;
                    MaximumVersion = SqlClientConstants.MicrosoftDataSqlClient.MaximumVersion;
                    IntegrationName = SqlClientConstants.SqlCommandIntegrationName;
                }
            }
        }
    }
}
