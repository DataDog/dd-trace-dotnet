using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
        /// <param name="method">The method.</param>
        /// <returns>The original methods return.</returns>
        public static object ExecuteReader(dynamic @this, int behavior, string method)
        {
            using (var scope = Tracer.Instance.StartActive(OperationName))
            {
                // set the scope properties
                // - row count is not supported so we don't set it
                scope.Span.ResourceName = @this.CommandText;
                scope.Span.Type = SpanTypes.Sql;
                scope.Span.SetTag(Tags.SqlDatabase, @this.Connection.ConnectionString);

                dynamic result;
                try
                {
                    result = @this.ExecuteReader(behavior, method);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }

                return result;
            }
        }
    }
}
