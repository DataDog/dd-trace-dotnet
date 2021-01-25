namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Npgsql
{
    internal static class NpgsqlConstants
    {
        public const string SqlCommandIntegrationName = "NpgsqlCommand";

        public static class Npgsql
        {
            public const string AssemblyName = "Npgsql";
            public const string SqlCommandType = "Npgsql.NpgsqlCommand";
            public const string MinimumVersion = "4.0.0";
            public const string MaximumVersion = "5.*.*";
            public const string SqlDataReaderType = "Npgsql.NpgsqlDataReader";
            public const string SqlDataReaderTaskType = "System.Threading.Tasks.Task`1<Npgsql.NpgsqlDataReader>";

            public class InstrumentSqlCommandAttribute : Datadog.Trace.ClrProfiler.InstrumentMethodAttribute
            {
                public InstrumentSqlCommandAttribute()
                {
                    Assembly = AssemblyName;
                    Type = NpgsqlConstants.Npgsql.SqlCommandType;
                    MinimumVersion = NpgsqlConstants.Npgsql.MinimumVersion;
                    MaximumVersion = NpgsqlConstants.Npgsql.MaximumVersion;
                    IntegrationName = NpgsqlConstants.SqlCommandIntegrationName;
                }
            }
        }
    }
}
