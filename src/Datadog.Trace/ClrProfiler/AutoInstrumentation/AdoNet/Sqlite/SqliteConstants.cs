// <copyright file="SqliteConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Sqlite
{
    internal static class SqliteConstants
    {
        public const string SqlCommandIntegrationName = "SqliteCommand";

        internal struct MicrosoftDataSqliteClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "Microsoft.Data.Sqlite";

            public string SqlCommandType => "Microsoft.Data.Sqlite.SqliteCommand";

            public string MinimumVersion => "2.0.0";

            public string MaximumVersion => "5.*.*";

            public string DataReaderType => "Microsoft.Data.Sqlite.SqliteDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<Microsoft.Data.Sqlite.SqliteDataReader>";
        }

        internal struct SystemDataSqliteClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "System.Data.SQLite";

            public string SqlCommandType => "System.Data.SQLite.SQLiteCommand";

            public string MinimumVersion => "1.0.0";

            public string MaximumVersion => "2.*.*";

            public string DataReaderType => "System.Data.SQLite.SQLiteDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<System.Data.SQLite.SQLiteDataReader>";
        }
    }
}
