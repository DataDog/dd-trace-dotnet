using System;
using System.Data;
using System.Data.Common;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// SqlServer handles tracing System.Data.SqlClient
    /// </summary>
    public static class SqlServer
    {
        internal const string OperationName = "sql-server.query";
        internal const string ServiceName = "sql-server";

        /// <summary>
        /// ExecuteReader traces any SQL call.
        /// </summary>
        /// <param name="this">The "this" pointer for the method call.</param>
        /// <param name="behavior">The behavior.</param>
        /// <param name="method">The method.</param>
        /// <returns>The original methods return.</returns>
        [InterceptMethod(
            CallerAssembly = "System.Data",
            CallerType = "System.Data.SqlClient.SqlCommand",
            TargetAssembly = "System.Data",
            TargetType = "System.Data.SqlClient.SqlCommand",
            TargetMethod = "ExecuteReader",
            TargetSignature = "20 02 0C 52 08 0B 52 5B 0E")]
        public static object ExecuteReaderWithMethod(dynamic @this, int behavior, string method)
        {
            var command = (DbCommand)@this;

            var executeReaderWithMethod = DynamicMethodBuilder<Func<object, CommandBehavior, string, object>>
               .GetOrCreateMethodCallDelegate(
                    command.GetType(),
                    "ExecuteReader"
                    //methodParameterTypes:new[] { typeof(CommandBehavior), typeof(string) }
                );

            using (var scope = CreateScope(command))
            {
                try
                {
                    return executeReaderWithMethod(command, (CommandBehavior)behavior, method);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// ExecuteReader traces any SQL call.
        /// </summary>
        /// <param name="this">The "this" pointer for the method call.</param>
        /// <param name="behavior">The behavior.</param>
        /// <returns>The original methods return.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data.SqlClient",
            TargetType = "System.Data.SqlClient.SqlCommand",
            TargetMethod = "ExecuteReader",
            TargetSignature = "20 01 0C 57 0C 0B 5C")]
        public static object ExecuteReader(dynamic @this, int behavior)
        {
            var command = (DbCommand)@this;

            var executeReader = DynamicMethodBuilder<Func<object, CommandBehavior, object>>
               .GetOrCreateMethodCallDelegate(
                    command.GetType(),
                    "ExecuteReader"
                    // new[] { typeof(CommandBehavior) }
                );

            using (var scope = CreateScope(command))
            {
                try
                {
                    return executeReader(command, (CommandBehavior)behavior);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static Scope CreateScope(DbCommand command)
        {
            Tracer tracer = Tracer.Instance;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            var scope = tracer.StartActive(OperationName, serviceName: serviceName);
            scope.Span.SetTag(Tags.DbType, "mssql");
            scope.Span.AddTagsFromDbCommand(command);
            return scope;
        }
    }
}
