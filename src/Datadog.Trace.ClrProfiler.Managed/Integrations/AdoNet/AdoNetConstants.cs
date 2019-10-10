namespace Datadog.Trace.ClrProfiler.Integrations
{
    internal static class AdoNetConstants
    {
        public static class AssemblyNames
        {
            // .NET Framework
            public const string SystemData = "System.Data";

            // .NET Core
            public const string SystemDataCommon = "System.Data.Common";
            public const string SystemDataSqlClient = "System.Data.SqlClient";
        }

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
