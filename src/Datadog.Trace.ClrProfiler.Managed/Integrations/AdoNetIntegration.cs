using System;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// AdoNetIntegration provides methods that add tracing to ADO.NET calls.
    /// </summary>
    public static class AdoNetIntegration
    {
        private const string IntegrationName = "AdoNet";
        private const string Major4 = "4";
        private const string FrameworkAssembly = "System.Data";
        private const string CoreAssembly = "System.Data.Common";
        private const string SystemDataCommonDbCommand = "System.Data.Common.DbCommand";
        private const string SystemDataCommonDbDataReader = "System.Data.Common.DbDataReader";
        private const string SystemDataCommonCommandBehavior = "System.Data.CommandBehavior";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(AdoNetIntegration));

        /// <summary>
        /// Wrapper method that instruments <see cref="System.Data.Common.DbCommand.ExecuteDbDataReader"/>.
        /// </summary>
        /// <param name="this">The <see cref="DbCommand"/> that is references by the "this" pointer in the instrumented method.</param>
        /// <param name="behavior">A value from <see cref="CommandBehavior"/>.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = FrameworkAssembly, // .NET Framework
            TargetType = SystemDataCommonDbCommand,
            TargetSignatureTypes = new[] { SystemDataCommonDbDataReader, SystemDataCommonCommandBehavior },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssembly = CoreAssembly, // .NET Core
            TargetType = SystemDataCommonDbCommand,
            TargetSignatureTypes = new[] { SystemDataCommonDbDataReader, SystemDataCommonCommandBehavior },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteDbDataReader(
            object @this,
            int behavior,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<object, CommandBehavior, object> instrumentedMethod = null;
            var commandBehavior = (CommandBehavior)behavior;

            try
            {
                var instrumentedType = typeof(DbCommand);
                instrumentedMethod =
                    MethodBuilder<Func<object, CommandBehavior, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(ExecuteDbDataReader))
                       .WithConcreteType(instrumentedType)
                       .WithParameters(commandBehavior)
                       .WithNamespaceAndNameFilters(SystemDataCommonDbDataReader, SystemDataCommonCommandBehavior)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error resolving {SystemDataCommonDbCommand}.{nameof(ExecuteDbDataReader)}(...)", ex);
                throw;
            }

            using (var scope = CreateScope((DbCommand)@this))
            {
                try
                {
                    return instrumentedMethod(@this, commandBehavior);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
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
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = FrameworkAssembly, // .NET Framework
            TargetType = SystemDataCommonDbCommand,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Data.Common.DbDataReader>", SystemDataCommonCommandBehavior, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssembly = CoreAssembly, // .NET Core
            TargetType = SystemDataCommonDbCommand,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Data.Common.DbDataReader>", SystemDataCommonCommandBehavior, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteDbDataReaderAsync(
            object @this,
            int behavior,
            object cancellationTokenSource,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            var instrumentedType = typeof(DbCommand);
            var dataReaderType = typeof(DbDataReader);
            var commandBehavior = (CommandBehavior)behavior;
            Func<object, CommandBehavior, object, object> instrumentedMethod = null;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, CommandBehavior, object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(ExecuteDbDataReaderAsync))
                       .WithConcreteType(instrumentedType)
                       .WithParameters(commandBehavior, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, SystemDataCommonCommandBehavior, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorException($"Error resolving {SystemDataCommonDbCommand}.{nameof(ExecuteDbDataReaderAsync)}(...)", ex);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                owningType: instrumentedType,
                taskResultType: dataReaderType,
                nameOfIntegrationMethod: nameof(ExecuteDbDataReaderAsyncInternal),
                integrationType: typeof(AdoNetIntegration),
                parametersToPass: new object[] { @this, behavior, cancellationToken, instrumentedMethod });
        }

        private static async Task<T> ExecuteDbDataReaderAsyncInternal<T>(
            DbCommand command,
            CommandBehavior behavior,
            CancellationToken cancellationToken,
            Func<object, CommandBehavior, object, object> instrumentedMethod)
        {
            using (var scope = CreateScope(command))
            {
                try
                {
                    var task = (Task<T>)instrumentedMethod(command, behavior, cancellationToken);
                    return await task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static Scope CreateScope(DbCommand command)
        {
            Scope scope = null;

            try
            {
                Tracer tracer = Tracer.Instance;

                if (!tracer.Settings.IsIntegrationEnabled(IntegrationName))
                {
                    // integration disabled, don't create a scope, skip this trace
                    return null;
                }

                string dbType = GetDbType(command.GetType().Name);

                if (dbType == null)
                {
                    // don't create a scope, skip this trace
                    return null;
                }

                string serviceName = $"{tracer.DefaultServiceName}-{dbType}";
                string operationName = $"{dbType}.query";

                scope = tracer.StartActive(operationName, serviceName: serviceName);
                var span = scope.Span;
                span.SetTag(Tags.DbType, dbType);
                span.AddTagsFromDbCommand(command);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: false);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }

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
                case "InterceptableDbCommand":
                case "ProfiledDbCommand":
                    // don't create spans for these
                    return null;
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
