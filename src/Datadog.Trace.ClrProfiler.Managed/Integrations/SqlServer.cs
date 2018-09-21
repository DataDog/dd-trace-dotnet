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
        private const string OperationName = "sqlserver.query";
        private static Func<object, CommandBehavior, object> _executeReader;
        private static Func<object, CommandBehavior, string, object> _executeReaderWithMethod;

        /// <summary>
        /// ExecuteReader traces any SQL call.
        /// </summary>
        /// <param name="this">The "this" pointer for the method call.</param>
        /// <param name="behavior">The behavior.</param>
        /// <param name="method">The method.</param>
        /// <returns>The original methods return.</returns>
        public static object ExecuteReaderWithMethod(dynamic @this, int behavior, string method)
        {
            var command = (DbCommand)@this;

            if (_executeReaderWithMethod == null)
            {
                _executeReaderWithMethod = DynamicMethodBuilder<Func<object, CommandBehavior, string, object>>.CreateMethodCallDelegate(
                    command.GetType(),
                    "ExecuteReader",
                    new Type[] { typeof(CommandBehavior), typeof(string) });
            }

            using (var scope = CreateScope(command))
            {
                try
                {
                    return _executeReaderWithMethod(command, (CommandBehavior)behavior, method);
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
        public static object ExecuteReader(dynamic @this, int behavior)
        {
            var command = (DbCommand)@this;

            if (_executeReader == null)
            {
                _executeReader = DynamicMethodBuilder<Func<object, CommandBehavior, object>>.CreateMethodCallDelegate(
                    command.GetType(),
                    "ExecuteReader",
                    new Type[] { typeof(CommandBehavior) });
            }

            using (var scope = CreateScope(command))
            {
                try
                {
                    return _executeReader(command, (CommandBehavior)behavior);
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
            var scope = Tracer.Instance.StartActive(OperationName);
            scope.Span.SetTag(Tags.DbType, "mssql");
            scope.Span.AddTagsFromDbCommand(command);
            return scope;
        }
    }
}
