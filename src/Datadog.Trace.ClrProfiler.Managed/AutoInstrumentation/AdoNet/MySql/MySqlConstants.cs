// <copyright file="MySqlConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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

            public string MinimumVersion => "6.7.0";

            public string MaximumVersion => "6.*.*";

            public string DataReaderType => "MySql.Data.MySqlClient.MySqlDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<MySql.Data.MySqlClient.MySqlDataReader>";
        }

        internal struct MySqlData8ClientData : IAdoNetClientData
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
