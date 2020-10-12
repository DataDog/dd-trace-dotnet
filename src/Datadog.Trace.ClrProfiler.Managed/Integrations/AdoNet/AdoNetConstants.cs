namespace Datadog.Trace.ClrProfiler.Integrations.AdoNet
{
    internal static class AdoNetConstants
    {
        internal const string IntegrationName = "AdoNet";

        public static class AssemblyNames
        {
            // .NET Framework
            public const string SystemData = "System.Data";

            // .NET Core
            public const string SystemDataCommon = "System.Data.Common";
            public const string SystemDataSqlClient = "System.Data.SqlClient";

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
