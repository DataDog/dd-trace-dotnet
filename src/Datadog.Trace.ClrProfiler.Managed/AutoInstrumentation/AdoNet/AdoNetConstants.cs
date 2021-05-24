// <copyright file="AdoNetConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class AdoNetConstants
    {
        internal const string IntegrationName = nameof(IntegrationIds.AdoNet);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        internal struct SystemDataClientData : IAdoNetClientData
        {
            public string IntegrationName => AdoNet.AdoNetConstants.IntegrationName;

            public string AssemblyName => "System.Data";

            public string SqlCommandType => "System.Data.Common.DbCommand";

            public string MinimumVersion => "4.0.0";

            public string MaximumVersion => "4.*.*";

            public string DataReaderType => AdoNet.AdoNetConstants.TypeNames.DbDataReaderType;

            public string DataReaderTaskType => AdoNet.AdoNetConstants.TypeNames.DbDataReaderTaskType;
        }

        internal struct SystemDataCommonClientData : IAdoNetClientData
        {
            public string IntegrationName => AdoNet.AdoNetConstants.IntegrationName;

            public string AssemblyName => "System.Data.Common";

            public string SqlCommandType => "System.Data.Common.DbCommand";

            public string MinimumVersion => "4.0.0";

            public string MaximumVersion => "5.*.*";

            public string DataReaderType => AdoNet.AdoNetConstants.TypeNames.DbDataReaderType;

            public string DataReaderTaskType => AdoNet.AdoNetConstants.TypeNames.DbDataReaderTaskType;
        }

        public static class TypeNames
        {
            public const string CommandBehavior = "System.Data.CommandBehavior";

            public const string DbDataReaderType = "System.Data.Common.DbDataReader";
            public const string DbDataReaderTaskType = "System.Threading.Tasks.Task`1<System.Data.Common.DbDataReader>";

            public const string Int32TaskType = "System.Threading.Tasks.Task`1<System.Int32>";
            public const string ObjectTaskType = "System.Threading.Tasks.Task`1<System.Object>";
        }

        public static class MethodNames
        {
            public const string ExecuteNonQuery = "ExecuteNonQuery";
            public const string ExecuteNonQueryAsync = "ExecuteNonQueryAsync";

            public const string ExecuteScalar = "ExecuteScalar";
            public const string ExecuteScalarAsync = "ExecuteScalarAsync";

            public const string ExecuteReader = "ExecuteReader";
            public const string ExecuteReaderAsync = "ExecuteReaderAsync";

            public const string ExecuteDbDataReader = "ExecuteDbDataReader";
            public const string ExecuteDbDataReaderAsync = "ExecuteDbDataReaderAsync";
        }
    }
}
