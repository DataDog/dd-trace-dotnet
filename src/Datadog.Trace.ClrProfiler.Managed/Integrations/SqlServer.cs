using System;
using System.Data;
using System.Data.Common;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// SqlServer handles tracing System.Data.SqlClient
    /// </summary>
    public static class SqlServer
    {
        private const string OperationName = "sqlserver.query";

        /// <summary>
        /// ExecuteReader traces any SQL call.
        /// </summary>
        /// <param name="this">The "this" pointer for the method call.</param>
        /// <param name="behavior">The behavior.</param>
        /// <returns>The original methods return.</returns>
        public static object ExecuteReader(dynamic @this, int behavior)
        {
            var command = (DbCommand)@this;

            using (var scope = Tracer.Instance.StartActive(OperationName))
            {
                // set the scope properties
                // - row count is not supported so we don't set it
                scope.Span.ResourceName = command.CommandText;
                scope.Span.Type = SpanTypes.Sql;
                scope.Span.SetTag(Tags.SqlDatabase, command.Connection?.ConnectionString);

                try
                {
                    var dynamicMethod = DynamicMethodBuilder.CreateMethodCallDelegate<Func<object, CommandBehavior, object>>(
                        command.GetType(),
                        "ExecuteReader",
                        false);

                    return dynamicMethod(command, (CommandBehavior)behavior);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }
    }
}
