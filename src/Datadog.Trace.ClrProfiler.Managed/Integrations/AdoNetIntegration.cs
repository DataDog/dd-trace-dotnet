using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// AdoNetIntegration provides methods that add tracing to ADO.NET calls.
    /// </summary>
    public static class AdoNetIntegration
    {
        // TODO: rename to DbCommandIntegration
        private const string IntegrationName = "AdoNet";
        private const string Major4 = "4";
        private const string FrameworkAssembly = "System.Data";
        private const string CoreAssembly = "System.Data.Common";
        private const string DbCommandTypeName = "System.Data.Common.DbCommand";
        private const string DbDataReaderTypeName = "System.Data.Common.DbDataReader";
        private const string CommandBehaviorTypeName = "System.Data.CommandBehavior";

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(AdoNetIntegration));

        /// <summary>
        /// Wrapper method that instruments <see cref="System.Data.Common.DbCommand.ExecuteDbDataReader"/>.
        /// </summary>
        /// <param name="command">The <see cref="DbCommand"/> that is references by the "this" pointer in the instrumented method.</param>
        /// <param name="behavior">A value from <see cref="CommandBehavior"/>.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { DbDataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteDbDataReader(
            object command,
            int behavior,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<DbCommand, CommandBehavior, DbDataReader> instrumentedMethod;
            var commandBehavior = (CommandBehavior)behavior;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, CommandBehavior, DbDataReader>>
                       .Start(moduleVersionPtr, mdToken, opCode, ExecuteDbDataReaderMethodName)
                       .WithConcreteType(typeof(DbCommand))
                       .WithParameters(commandBehavior)
                       .WithNamespaceAndNameFilters(DbDataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DbCommandTypeName,
                    methodName: nameof(ExecuteDbDataReader),
                    instanceType: @this.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as DbCommand;

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, dbCommand, IntegrationName))
            {
                try
                {
                    return instrumentedMethod(dbCommand, commandBehavior);
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
        /// <param name="command">The <see cref="DbCommand"/> that is references by the "this" pointer in the instrumented method.</param>
        /// <param name="behavior">A value from <see cref="CommandBehavior"/>.</param>
        /// <param name="cancellationTokenSource">A cancellation token source that can be used to cancel the async operation.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Data.Common.DbDataReader>", AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteDbDataReaderAsync(
            object command,
            int behavior,
            object cancellationTokenSource,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (@this == null)
            {
                throw new ArgumentNullException(nameof(@this));
            }

            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            return ExecuteDbDataReaderAsyncInternal(
                command as DbCommand,
                (CommandBehavior)behavior,
                cancellationToken,
                opCode,
                mdToken,
                moduleVersionPtr);
        }

        private static async Task<DbDataReader> ExecuteDbDataReaderAsyncInternal(
            DbCommand command,
            CommandBehavior commandBehavior,
            CancellationToken cancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<DbCommand, CommandBehavior, CancellationToken, Task<DbDataReader>> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, CommandBehavior, CancellationToken, Task<DbDataReader>>>
                       .Start(moduleVersionPtr, mdToken, opCode, ExecuteDbDataReaderAsyncMethodName)
                       .WithConcreteType(typeof(DbCommand))
                       .WithParameters(commandBehavior, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DbCommandTypeName,
                    methodName: nameof(ExecuteDbDataReaderAsync),
                    instanceType: @this.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, command, IntegrationName))
            {
                try
                {
                    return await instrumentedMethod(command, commandBehavior, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }
    }
}
