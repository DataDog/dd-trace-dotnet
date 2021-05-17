// <copyright file="NpgsqlConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
