// <copyright file="SqlClientConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.SqlClient
{
    internal static class SqlClientConstants
    {
        public const string SqlCommandIntegrationName = "SqlCommand";

        internal struct SystemDataAdoNetClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "System.Data";

            public string SqlCommandType => "System.Data.SqlClient.SqlCommand";

            public string MinimumVersion => "4.0.0";

            public string MaximumVersion => "4.*.*";

            public string DataReaderType => "System.Data.SqlClient.SqlDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<System.Data.SqlClient.SqlDataReader>";
        }

        internal struct SystemDataSqlClientAdoNetClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "System.Data.SqlClient";

            public string SqlCommandType => "System.Data.SqlClient.SqlCommand";

            public string MinimumVersion => "4.0.0";

            public string MaximumVersion => "4.*.*";

            public string DataReaderType => "System.Data.SqlClient.SqlDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<System.Data.SqlClient.SqlDataReader>";
        }

        internal struct MicrosoftDataAdoNetClientData : IAdoNetClientData
        {
            public string IntegrationName => SqlCommandIntegrationName;

            public string AssemblyName => "Microsoft.Data.SqlClient";

            public string SqlCommandType => "Microsoft.Data.SqlClient.SqlCommand";

            public string MinimumVersion => "1.0.0";

            public string MaximumVersion => "3.*.*";

            public string DataReaderType => "Microsoft.Data.SqlClient.SqlDataReader";

            public string DataReaderTaskType => "System.Threading.Tasks.Task`1<Microsoft.Data.SqlClient.SqlDataReader>";
        }
    }
}
