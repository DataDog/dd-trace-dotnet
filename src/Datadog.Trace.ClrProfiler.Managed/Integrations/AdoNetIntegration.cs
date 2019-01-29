using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// SqlServer handles tracing System.Data.SqlClient
    /// </summary>
    public static class AdoNetIntegration
    {
        /// <summary>
        /// Wrapper method that instruments <see cref="System.Data.Common.DbCommand.ExecuteDbDataReader"/>.
        /// </summary>
        /// <param name="this">The <see cref="DbCommand"/> that is references by the "this" pointer in the instrumented method.</param>
        /// <param name="behavior">A value from <see cref="CommandBehavior"/>.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data", // .NET Framework
            TargetType = "System.Data.Common.DbCommand")]
        [InterceptMethod(
            TargetAssembly = "System.Data.Common", // .NET Core
            TargetType = "System.Data.Common.DbCommand")]
        public static object ExecuteDbDataReader(object @this, int behavior)
        {
            var command = (DbCommand)@this;
            var commandBehavior = (CommandBehavior)behavior;

            var executeReader = DynamicMethodBuilder<Func<object, CommandBehavior, object>>
               .GetOrCreateMethodCallDelegate(
                    command.GetType(),
                    "ExecuteDbDataReader");

            using (var scope = CreateScope(command))
            {
                try
                {
                    return executeReader(command, commandBehavior);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Wrapper method that instruments <see cref="System.Data.Common.DbCommand.ExecuteDbDataReader"/>.
        /// </summary>
        /// <param name="this">The <see cref="DbCommand"/> that is references by the "this" pointer in the instrumented method.</param>
        /// <param name="behavior">A value from <see cref="CommandBehavior"/>.</param>
        /// <param name="cancellationTokenSource">A cancellation token source that can be used to cancel the async operation.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data", // .NET Framework
            TargetType = "System.Data.Common.DbCommand")]
        [InterceptMethod(
            TargetAssembly = "System.Data.Common", // .NET Core
            TargetType = "System.Data.Common.DbCommand")]
        public static object ExecuteDbDataReaderAsync(object @this, int behavior, object cancellationTokenSource)
        {
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            var command = (DbCommand)@this;
            var commandBehavior = (CommandBehavior)behavior;

            return ExecuteDbDataReaderAsyncInternal(command, commandBehavior, cancellationToken);
        }

        private static async Task<object> ExecuteDbDataReaderAsyncInternal(DbCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            var executeReader = DynamicMethodBuilder<Func<object, CommandBehavior, CancellationToken, Task<object>>>
               .GetOrCreateMethodCallDelegate(
                    command.GetType(),
                    "ExecuteDbDataReaderAsync");

            using (var scope = CreateScope(command))
            {
                try
                {
                    return await executeReader(command, behavior, cancellationToken).ConfigureAwait(false);
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
            string dbType = GetDbType(command.GetType().Name);

            Tracer tracer = Tracer.Instance;
            string serviceName = $"{tracer.DefaultServiceName}-{dbType}";
            string operationName = $"{dbType}.query";

            var scope = tracer.StartActive(operationName, serviceName: serviceName);
            scope.Span.SetTag(Tags.DbType, dbType);
            scope.Span.AddTagsFromDbCommand(command);
            return scope;
        }

        private static string GetDbType(string commandTypeName)
        {
            switch (commandTypeName)
            {
                case "SqlCommand":
                    return "sql-server";
                case "NpgsqlCommand":
                    return "postgres";
                default:
                    const string commandSuffix = "Command";

                    // remove "Command" suffix if present
                    return commandTypeName.EndsWith(commandSuffix)
                               ? commandTypeName.Substring(0, commandTypeName.Length - commandSuffix.Length).ToLowerInvariant()
                               : commandTypeName.ToLowerInvariant();
            }
        }
    }
}
