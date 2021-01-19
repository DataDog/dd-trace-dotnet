using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
