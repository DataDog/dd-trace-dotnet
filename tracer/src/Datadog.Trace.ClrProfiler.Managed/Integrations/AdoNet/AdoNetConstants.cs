// <copyright file="AdoNetConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.Integrations.AdoNet
{
    internal static class AdoNetConstants
    {
        internal const string IntegrationName = nameof(IntegrationIds.AdoNet);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

        public static class AssemblyNames
        {
            // .NET Framework
            public const string SystemData = "System.Data";

            // .NET Core
            public const string SystemDataCommon = "System.Data.Common";

            // .NET Standard
            public const string NetStandard = "netstandard";
        }

        public static class TypeNames
        {
            // ReSharper disable InconsistentNaming
            public const string IDataReader = "System.Data.IDataReader";
            public const string IDbCommand = "System.Data.IDbCommand";
            // ReSharper restore InconsistentNaming

            public const string DbDataReader = "System.Data.Common.DbDataReader";
            public const string DbCommand = "System.Data.Common.DbCommand";
            public const string CommandBehavior = "System.Data.CommandBehavior";

            public const string DbDataReaderTask = "System.Threading.Tasks.Task`1<System.Data.Common.DbDataReader>";
        }

        public static class MethodNames
        {
            public const string ExecuteNonQuery = "ExecuteNonQuery";
            public const string ExecuteNonQueryAsync = "ExecuteNonQueryAsync";

            public const string ExecuteScalar = "ExecuteScalar";
            public const string ExecuteScalarAsync = "ExecuteScalarAsync";

            public const string ExecuteReader = "ExecuteReader";
            public const string ExecuteReaderAsync = "ExecuteReaderAsync";
        }
    }
}
