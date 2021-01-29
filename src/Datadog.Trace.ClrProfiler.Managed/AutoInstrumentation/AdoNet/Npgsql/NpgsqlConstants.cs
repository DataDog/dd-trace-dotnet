namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Npgsql
{
    internal static class NpgsqlConstants
    {
        public const string SqlCommandIntegrationName = "NpgsqlCommand";

        internal struct NpgsqlClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "Npgsql";

            public string SqlCommandType => "Npgsql.NpgsqlCommand";

            public string MinimumVersion => "4.0.0";

            public string MaximumVersion => "5.*.*";

            public string DataReaderType => "Npgsql.NpgsqlDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<Npgsql.NpgsqlDataReader>";
        }
    }
}
