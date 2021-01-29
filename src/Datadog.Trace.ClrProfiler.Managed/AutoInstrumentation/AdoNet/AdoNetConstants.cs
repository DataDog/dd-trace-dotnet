using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class AdoNetConstants
    {
        internal const string IntegrationName = nameof(IntegrationIds.AdoNet);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);

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
