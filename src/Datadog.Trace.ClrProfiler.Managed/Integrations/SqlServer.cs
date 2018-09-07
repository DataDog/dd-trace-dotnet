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

        private static object originalExecuteReaderLock = new object();
        private static MethodInfo originalExecuteReader;

        /// <summary>
        /// ExecuteReader traces any SQL call.
        /// </summary>
        /// <param name="this">The "this" pointer for the method call.</param>
        /// <param name="behavior">The behavior.</param>
        /// <param name="method">The method.</param>
        /// <returns>The original methods return.</returns>
        public static object ExecuteReader(dynamic @this, int behavior, string method)
        {
            var originalMethod = GetOriginalExecuteReader(@this);

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
                    result = originalMethod.Invoke(@this, new object[] { behavior, method });
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }

                return result;
            }
        }

        private static MethodInfo GetOriginalExecuteReader(dynamic @this)
        {
            if (originalExecuteReader != null)
            {
                return originalExecuteReader;
            }

            lock (originalExecuteReaderLock)
            {
                if (originalExecuteReader != null)
                {
                    return originalExecuteReader;
                }

                var systemDataAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(asm => asm.GetName().Name == "System.Data");
                var commandBehaviorType = systemDataAssembly.GetType("System.Data.CommandBehavior");

                Type thisType = @this.GetType();
                originalExecuteReader = thisType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).
                    Where(m => m.Name == "ExecuteReader").
                    Where(m => m.GetParameters().Length == 2).
                    First();

                return originalExecuteReader;
            }
        }
    }
}
